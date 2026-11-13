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

/** Something about what a widget shows that its user can change. */
export type WidgetSetting = WidgetChoiceSetting | WidgetToggleSetting

/**
 * Setting values by key, as strings. Stored values hold only what the user
 * changed; resolved values hold every setting the widget declares.
 */
export type WidgetSettingValues = Record<string, string>

const defaultOf = (setting: WidgetSetting): string =>
  setting.kind === 'toggle' ? String(setting.default) : setting.default

const accepts = (setting: WidgetSetting, value: string): boolean =>
  setting.kind === 'toggle'
    ? value === 'true' || value === 'false'
    : setting.options.some((o) => o.value === value)

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
        value !== undefined && accepts(setting, value)
          ? value
          : defaultOf(setting),
      ]
    }),
  )

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
