'use client'

import { Button, Segmented, Switch, Typography } from 'antd'
import { createContext, FC, useId } from 'react'
import styles from './home.module.css'
import { HomeWidget } from './widget-registry'
import {
  hasCustomSettings,
  resolveSettings,
  settingsToStore,
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
