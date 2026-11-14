'use client'

import { Button, Checkbox, Segmented, Skeleton, Switch, Typography } from 'antd'
import { createContext, FC, useId, useState } from 'react'
import styles from './home.module.css'
import { HomeWidget } from './widget-registry'
import {
  hasCustomSettings,
  MAX_SETTING_VALUE_LENGTH,
  multiSelectValue,
  pickedIdsOf,
  pickedValues,
  pickValue,
  resolveSettings,
  settingsToStore,
  WidgetPickOptions,
  WidgetPickSetting,
  WidgetSettingValues,
} from './widget-settings'

const { Text } = Typography

/** A widget's settings and how to change them, for the gear on its card. */
export interface WidgetSettingsAccess {
  widget: HomeWidget
  /** The values the user changed, by key. */
  stored: WidgetSettingValues
  /** Takes the values to store: only those that differ from their default. */
  onChange: (stored: WidgetSettingValues) => void
}

/**
 * Set by the page around each widget whose settings can be changed, so
 * `WidgetCard` offers them without every widget passing them along. Null away
 * from the page and while the layout cannot be saved.
 */
export const WidgetSettingsContext = createContext<WidgetSettingsAccess | null>(
  null,
)

export type WidgetSettingsFormProps = WidgetSettingsAccess

/**
 * Checkboxes for the entries a pick setting chooses from, every one checked
 * until the user narrows it, with a note on what a narrowed pick leaves out.
 */
const PickChecks: FC<{
  setting: WidgetPickSetting
  values: WidgetSettingValues
  options: WidgetPickOptions
  noteId: string
  onChange: (value: string) => void
}> = ({
  setting,
  values,
  options: { options, isLoading, isError },
  noteId,
  onChange,
}) => {
  const [tooMany, setTooMany] = useState(false)
  if (isLoading)
    return <Skeleton active title={false} paragraph={{ rows: 2 }} />
  if (isError || options.length === 0)
    return (
      <Text type="secondary" id={noteId}>
        {isError
          ? `Could not load your ${setting.noun}.`
          : `You have no ${setting.noun} to choose from.`}
      </Text>
    )
  const narrowed = pickedIdsOf(values, setting.key, options)
  const picked = narrowed.length > 0 ? narrowed : options.map((o) => o.value)
  const notShown = options.length - picked.length
  // Narrowed, yet every entry is still checked: one left since, so the pick
  // looks like all while it would still keep out one joined later.
  const keepsOutLater = narrowed.length > 0 && notShown === 0
  const pick = (next: string[]) => {
    const value = pickValue(options, next)
    setTooMany(value.length > MAX_SETTING_VALUE_LENGTH)
    if (value.length <= MAX_SETTING_VALUE_LENGTH) onChange(value)
  }
  const maxPicked = Math.floor(
    (MAX_SETTING_VALUE_LENGTH + 1) / (options[0].value.length + 1),
  )
  return (
    <>
      <Checkbox.Group
        className={styles.settingsChecks}
        // The last one picked cannot be cleared: a pick of nothing would show nothing.
        options={options.map((o) => ({
          ...o,
          disabled: picked.length === 1 && picked[0] === o.value,
        }))}
        value={picked}
        onChange={pick}
      />
      {/* Announced, so unchecking a box says what it now leaves out. */}
      <Text type="secondary" id={noteId} role="status">
        {tooMany
          ? `Choose up to ${maxPicked} of your ${setting.noun}, or all of them.`
          : notShown > 0
            ? `${notShown} of your ${options.length} ${setting.noun} ${notShown === 1 ? 'is' : 'are'} not shown, nor will any you join later.`
            : keepsOutLater
              ? `All your ${setting.noun} are shown, but any you join later will not be.`
              : null}
      </Text>
      {keepsOutLater ? (
        <Button
          type="link"
          size="small"
          className={styles.settingsReset}
          onClick={() => onChange('')}
        >
          Show any you join later too
        </Button>
      ) : null}
    </>
  )
}

/** A pick setting's label and checkboxes, once its list has loaded. */
const PickField: FC<{
  setting: WidgetPickSetting
  values: WidgetSettingValues
  labelId: string
  onChange: (value: string) => void
}> = ({ setting, values, labelId, onChange }) => {
  const { Options } = setting
  const noteId = `${labelId}-note`
  return (
    <Options>
      {(options) => (
        <div
          className={styles.settingsField}
          role="group"
          aria-labelledby={labelId}
          aria-describedby={noteId}
          aria-busy={options.isLoading}
        >
          <Text type="secondary" id={labelId}>
            {setting.label}
          </Text>
          <PickChecks
            setting={setting}
            values={values}
            options={options}
            noteId={noteId}
            onChange={onChange}
          />
        </div>
      )}
    </Options>
  )
}

/**
 * The controls for everything a widget declares in its `settings`, with a
 * reset once any differs from its default. Each change is reported at once.
 */
const WidgetSettingsForm: FC<WidgetSettingsFormProps> = ({
  widget,
  stored,
  onChange,
}) => {
  const formId = useId()
  const settings = widget.settings ?? []
  const values = resolveSettings(settings, stored)
  const set = (key: string, value: string) =>
    onChange(settingsToStore(settings, { ...values, [key]: value }))

  return (
    <div className={styles.settingsForm}>
      {settings.map((setting) => {
        const labelId = `${formId}-${setting.key}`
        if (setting.kind === 'pick') {
          return (
            <PickField
              key={setting.key}
              setting={setting}
              values={values}
              labelId={labelId}
              onChange={(value) => set(setting.key, value)}
            />
          )
        }
        if (setting.kind === 'multiSelect') {
          const picked = pickedValues(values[setting.key])
          return (
            <div
              key={setting.key}
              className={styles.settingsField}
              role="group"
              aria-labelledby={labelId}
            >
              <Text type="secondary" id={labelId}>
                {setting.label}
              </Text>
              <Checkbox.Group
                className={styles.settingsChecks}
                // The last one picked cannot be cleared: a pick of nothing would show nothing.
                options={setting.options.map((o) => ({
                  ...o,
                  disabled: picked.length === 1 && picked[0] === o.value,
                }))}
                value={picked}
                onChange={(next) =>
                  set(setting.key, multiSelectValue(setting, next))
                }
              />
            </div>
          )
        }
        return setting.kind === 'toggle' ? (
          <div key={setting.key} className={styles.settingsToggle}>
            <Text id={labelId}>{setting.label}</Text>
            <Switch
              size="small"
              checked={values[setting.key] === 'true'}
              onChange={(checked) => set(setting.key, String(checked))}
              aria-labelledby={labelId}
            />
          </div>
        ) : (
          <div key={setting.key} className={styles.settingsField}>
            <Text type="secondary" id={labelId}>
              {setting.label}
            </Text>
            <Segmented
              size="small"
              options={setting.options}
              value={values[setting.key]}
              onChange={(value) => set(setting.key, value)}
              aria-labelledby={labelId}
            />
          </div>
        )
      })}
      {hasCustomSettings(settings, stored) && (
        <Button
          type="link"
          size="small"
          className={styles.settingsReset}
          onClick={() => onChange({})}
        >
          Reset {widget.title} to default
        </Button>
      )}
    </div>
  )
}

export default WidgetSettingsForm
