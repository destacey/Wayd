import type { FC, ReactNode } from 'react'

/** One of the values a choice setting offers. */
export interface WidgetSettingOption {
  /** What is stored; never changed once shipped. */
  value: string
  label: string
}

/** A setting the user picks one value for, from a fixed list. */
export interface WidgetChoiceSetting {
  kind: 'choice'
  /** The key the value is stored under; like a widget id, never renamed or reused once shipped. */
  key: string
  label: string
  options: WidgetSettingOption[]
  /** The value in effect until the user picks another; one of the options. */
  default: string
}

/** A setting that is on or off, stored as `'true'` or `'false'`. */
export interface WidgetToggleSetting {
  kind: 'toggle'
  /** The key the value is stored under; like a widget id, never renamed or reused once shipped. */
  key: string
  label: string
  default: boolean
}

/**
 * A setting the user picks one or more values for, from a fixed list. Stored
 * as the picked values joined by {@link MULTI_SELECT_SEPARATOR}, in option
 * order, so option values never contain it.
 */
export interface WidgetMultiSelectSetting {
  kind: 'multiSelect'
  /** The key the value is stored under; like a widget id, never renamed or reused once shipped. */
  key: string
  label: string
  options: WidgetSettingOption[]
  /** The values picked until the user picks others; each one of the options, at least one. */
  default: string[]
}

/** What a pick setting's form chooses from, as the widget loads it. */
export interface WidgetPickOptions {
  options: WidgetSettingOption[]
  isLoading: boolean
  /** True when the list could not be loaded, so the form says so rather than offering nothing. */
  isError?: boolean
}

/** A component that loads a pick setting's list and hands it to `children`. */
export type WidgetPickOptionsSource = FC<{
  children: (options: WidgetPickOptions) => ReactNode
}>

/**
 * A setting that picks some of a list the widget supplies at runtime, such as
 * the caller's teams. Nothing is stored until the user narrows it, so by
 * default every entry shows, one added later included; once narrowed, it holds
 * the ids picked, joined by {@link MULTI_SELECT_SEPARATOR}, and one added later
 * is not shown. An id no longer on the list is ignored, and when none of the
 * stored ids is, every entry shows.
 */
export interface WidgetPickSetting {
  kind: 'pick'
  /** The key the value is stored under; like a widget id, never renamed or reused once shipped. */
  key: string
  label: string
  /** What the list is of, plural and lower case ("teams"), for the form's note on what is not shown. */
  noun: string
  /**
   * Loads the list for the settings form. A component rather than a hook, so
   * the form never calls a hook it is handed.
   */
  Options: WidgetPickOptionsSource
}

/** Something about what a widget shows that its user can change. */
export type WidgetSetting =
  | WidgetChoiceSetting
  | WidgetToggleSetting
  | WidgetMultiSelectSetting
  | WidgetPickSetting

/**
 * Setting values by key, as strings. Stored values hold only what the user
 * changed; resolved values hold every setting the widget declares.
 */
export type WidgetSettingValues = Record<string, string>

/**
 * The longest value one setting may store, as the server's
 * `UpdateHomeLayoutRequest.MaxSettingValueLength` allows; a longer one fails
 * the whole layout's save. A pick of ids is the only setting that could reach it.
 */
export const MAX_SETTING_VALUE_LENGTH = 2048

/** What joins a multi-select setting's values in the one string it is stored as. */
export const MULTI_SELECT_SEPARATOR = ','

/**
 * The values a multi-select stores, in option order, without duplicates or
 * values the setting does not offer — so the same pick is always the same
 * string, and a default compares equal however it was reached.
 */
const joinPicked = (
  setting: WidgetMultiSelectSetting,
  values: Iterable<string>,
): string => {
  const picked = new Set(values)
  return setting.options
    .filter((o) => picked.has(o.value))
    .map((o) => o.value)
    .join(MULTI_SELECT_SEPARATOR)
}

const defaultOf = (setting: WidgetSetting): string => {
  switch (setting.kind) {
    case 'toggle':
      return String(setting.default)
    case 'multiSelect':
      return joinPicked(setting, setting.default)
    case 'pick':
      return ''
    default:
      return setting.default
  }
}

/**
 * The stored value in effect for a setting, or undefined when the setting no
 * longer accepts it. A multi-select keeps the values it still offers, so an
 * option dropped later does not undo the rest of the user's pick.
 */
const acceptedValue = (
  setting: WidgetSetting,
  value: string,
): string | undefined => {
  switch (setting.kind) {
    case 'toggle':
      return value === 'true' || value === 'false' ? value : undefined
    case 'multiSelect':
      return (
        joinPicked(setting, value.split(MULTI_SELECT_SEPARATOR)) || undefined
      )
    // Its list is known only at runtime, so the ids are checked where it is read.
    case 'pick':
      return value
    default:
      return setting.options.some((o) => o.value === value) ? value : undefined
  }
}

/**
 * Every declared setting's value in effect: the stored one when the setting
 * still accepts it, otherwise the default. Stored keys the widget does not
 * declare are left out.
 */
export const resolveSettings = (
  settings: WidgetSetting[] | undefined,
  stored: WidgetSettingValues | undefined,
): WidgetSettingValues =>
  Object.fromEntries(
    (settings ?? []).map((setting) => {
      const value = stored?.[setting.key]
      return [
        setting.key,
        (value !== undefined && acceptedValue(setting, value)) ||
          defaultOf(setting),
      ]
    }),
  )

/** A resolved multi-select value as the values picked. */
export const pickedValues = (value: string | undefined): string[] =>
  value ? value.split(MULTI_SELECT_SEPARATOR) : []

/** The values picked, as the one string a multi-select setting stores. */
export const multiSelectValue = (
  setting: WidgetMultiSelectSetting,
  picked: string[],
): string => joinPicked(setting, picked)

/**
 * The values worth storing: those that differ from their default. A default
 * that later changes then reaches everyone who never chose otherwise.
 */
export const settingsToStore = (
  settings: WidgetSetting[] | undefined,
  values: WidgetSettingValues | undefined,
): WidgetSettingValues => {
  const resolved = resolveSettings(settings, values)
  return Object.fromEntries(
    (settings ?? [])
      .filter((setting) => resolved[setting.key] !== defaultOf(setting))
      .map((setting) => [setting.key, resolved[setting.key]]),
  )
}

/** Whether any stored value changes what the widget shows. */
export const hasCustomSettings = (
  settings: WidgetSetting[] | undefined,
  stored: WidgetSettingValues | undefined,
): boolean => Object.keys(settingsToStore(settings, stored)).length > 0

/** The key of the setting every list widget shares. */
export const ROWS_SETTING_KEY = 'rows'

const ROW_CHOICES = [3, 5, 8, 10, 15]

/**
 * The most rows the setting offers. A widget that asks the server for one page
 * asks for this many and shows the first few, so changing the setting needs no
 * new request.
 */
export const MAX_ROWS = Math.max(...ROW_CHOICES)

/**
 * How many rows a list widget shows, defaulting to the number it showed
 * before the user could choose.
 */
export const rowsSetting = (
  defaultRows: number,
  label = 'Rows to show',
): WidgetChoiceSetting => ({
  kind: 'choice',
  key: ROWS_SETTING_KEY,
  label,
  options: ROW_CHOICES.map((rows) => ({
    value: String(rows),
    label: String(rows),
  })),
  default: String(defaultRows),
})

/** The key of the setting every widget with an empty state shares. */
export const EMPTY_SETTING_KEY = 'empty'

/**
 * What a widget does when it has nothing to show: explain how it fills, say so
 * in one line beside its title, or leave the page.
 */
export type EmptyDisplay = 'explain' | 'line' | 'hide'

const EMPTY_DISPLAYS: EmptyDisplay[] = ['explain', 'line', 'hide']

/**
 * What a widget does when it has nothing to show. Explain is the default, so a
 * new user learns how each widget fills; Hide is only ever the user's choice.
 */
export const emptySetting: WidgetChoiceSetting = {
  kind: 'choice',
  key: EMPTY_SETTING_KEY,
  label: 'When there is nothing to show',
  options: [
    { value: 'explain', label: 'Explain' },
    { value: 'line', label: 'One line' },
    { value: 'hide', label: 'Hide' },
  ],
  default: 'explain',
}

/** The empty display in effect: the setting's value, or Explain for a widget rendered without settings. */
export const emptyDisplayOf = (
  settings: WidgetSettingValues | undefined,
): EmptyDisplay => {
  const value = settings?.[EMPTY_SETTING_KEY]
  return EMPTY_DISPLAYS.find((d) => d === value) ?? 'explain'
}

/**
 * The rows a list widget shows: its setting's value, or `defaultRows` for a
 * widget rendered without settings.
 */
export const rowsToShow = (
  settings: WidgetSettingValues | undefined,
  defaultRows: number,
): number => {
  const rows = Number(settings?.[ROWS_SETTING_KEY])
  return Number.isInteger(rows) && rows > 0 ? rows : defaultRows
}

/**
 * A choice of how many days a widget looks ahead or back, each labelled
 * "14 days".
 */
export const daysSetting = (
  key: string,
  label: string,
  days: number[],
  defaultDays: number,
): WidgetChoiceSetting => ({
  kind: 'choice',
  key,
  label,
  options: days.map((d) => ({ value: String(d), label: `${d} days` })),
  default: String(defaultDays),
})

/**
 * The days a window setting is set to, or `defaultDays` for a widget rendered
 * without settings.
 */
export const daysOf = (
  settings: WidgetSettingValues | undefined,
  key: string,
  defaultDays: number,
): number => {
  const days = Number(settings?.[key])
  return Number.isInteger(days) && days > 0 ? days : defaultDays
}

/**
 * Whether a toggle setting is on, or `defaultOn` for a widget rendered without
 * settings.
 */
export const isOn = (
  settings: WidgetSettingValues | undefined,
  key: string,
  defaultOn: boolean,
): boolean => {
  const value = settings?.[key]
  return value === undefined ? defaultOn : value === 'true'
}

/**
 * The values a multi-select setting has picked, or `defaults` for a widget
 * rendered without settings.
 */
export const pickedOf = (
  settings: WidgetSettingValues | undefined,
  key: string,
  defaults: string[],
): string[] => {
  const picked = pickedValues(settings?.[key])
  return picked.length > 0 ? picked : defaults
}

/**
 * The ids a pick setting narrows to that are still on its list, or an empty
 * list for every entry: when nothing is stored, or none of the stored ids is
 * still there.
 */
export const pickedIdsOf = (
  settings: WidgetSettingValues | undefined,
  key: string,
  available?: { value: string }[],
): string[] => {
  const stored = pickedValues(settings?.[key])
  if (!available) return stored
  const ids = new Set(available.map((a) => a.value))
  return stored.filter((id) => ids.has(id))
}

/**
 * What a pick setting stores for the ids picked: nothing when every entry is
 * picked, so one added later shows too; otherwise the ids in list order.
 */
export const pickValue = (
  options: WidgetSettingOption[],
  picked: string[],
): string => {
  const chosen = new Set(picked)
  const kept = options.filter((o) => chosen.has(o.value))
  return kept.length === options.length
    ? ''
    : kept.map((o) => o.value).join(MULTI_SELECT_SEPARATOR)
}
