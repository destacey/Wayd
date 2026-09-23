'use client'

import { SettingOutlined } from '@ant-design/icons'
import {
  Button,
  Divider,
  Flex,
  InputNumber,
  Popover,
  Tag,
  Typography,
} from 'antd'
import { FC, useState } from 'react'
import { BacklogHealthThresholds } from '@/src/services/wayd-api'
import {
  BacklogHealthSettings,
  EMPTY_BACKLOG_HEALTH_SETTINGS,
  THRESHOLD_GROUPS,
  ThresholdKey,
  hasOverrides,
  thresholdConflicts,
} from './backlog-health-settings'

const { Text } = Typography

export interface BacklogHealthSettingsPopoverProps {
  settings: BacklogHealthSettings
  /** The values the current report used, shown as the starting point. */
  effective?: { lookbackDays: number; thresholds: BacklogHealthThresholds }
  onChange: (settings: BacklogHealthSettings) => void
}

interface Draft {
  lookbackDays: number
  thresholds: BacklogHealthThresholds
}

/**
 * Edits a copy and applies it on demand, so exploring several values makes
 * one request rather than one per keystroke.
 */
const BacklogHealthSettingsPopover: FC<BacklogHealthSettingsPopoverProps> = ({
  settings,
  effective,
  onChange,
}) => {
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<Draft | undefined>()

  const onOpenChange = (next: boolean) => {
    if (next && effective)
      setDraft({
        lookbackDays: effective.lookbackDays,
        thresholds: { ...effective.thresholds },
      })
    setOpen(next)
  }

  // A value counts as an override once changed, even if changed back to the
  // default: the defaults are the server's, not known here.
  const apply = () => {
    if (!draft || !effective) return
    const thresholds = { ...settings.thresholds }
    for (const key of Object.keys(draft.thresholds) as ThresholdKey[]) {
      if (draft.thresholds[key] !== effective.thresholds[key])
        thresholds[key] = draft.thresholds[key]
    }
    onChange({
      lookbackDays:
        draft.lookbackDays !== effective.lookbackDays
          ? draft.lookbackDays
          : settings.lookbackDays,
      thresholds,
    })
    setOpen(false)
  }

  const reset = () => {
    onChange(EMPTY_BACKLOG_HEALTH_SETTINGS)
    setOpen(false)
  }

  const conflicts = draft ? thresholdConflicts(draft.thresholds) : []

  const setThreshold = (key: ThresholdKey, value: number | null) => {
    if (value === null || !draft) return
    setDraft({ ...draft, thresholds: { ...draft.thresholds, [key]: value } })
  }

  const content = draft && (
    <Flex vertical gap="small" style={{ maxWidth: 420 }}>
      <Flex justify="space-between" align="center" gap="small">
        <Text>History</Text>
        <InputNumber
          aria-label="History"
          min={14}
          max={365}
          step={1}
          value={draft.lookbackDays}
          onChange={(value) =>
            value !== null && setDraft({ ...draft, lookbackDays: value })
          }
          suffix="days"
          style={{ width: 150 }}
        />
      </Flex>
      {THRESHOLD_GROUPS.map((group) => (
        <Flex key={group.title} vertical gap="small">
          <Divider titlePlacement="start" style={{ margin: '4px 0' }}>
            {group.title}
          </Divider>
          {group.fields.map((field) => (
            <Flex
              key={field.key}
              justify="space-between"
              align="center"
              gap="small"
            >
              <Text>{field.label}</Text>
              <InputNumber
                aria-label={field.label}
                min={field.min}
                max={field.max}
                step={field.step}
                value={draft.thresholds[field.key]}
                onChange={(value) => setThreshold(field.key, value)}
                suffix={field.suffix}
                style={{ width: 150 }}
              />
            </Flex>
          ))}
        </Flex>
      ))}
      {conflicts.map((conflict) => (
        <Text key={conflict} type="danger">
          {conflict}
        </Text>
      ))}
      <Flex justify="end" gap="small" style={{ marginTop: 8 }}>
        <Button onClick={reset} disabled={!hasOverrides(settings)}>
          Reset to defaults
        </Button>
        <Button type="primary" onClick={apply} disabled={conflicts.length > 0}>
          Apply
        </Button>
      </Flex>
    </Flex>
  )

  return (
    <Flex gap="small" align="center">
      {hasOverrides(settings) && (
        <Tag color="processing">Custom thresholds</Tag>
      )}
      <Popover
        trigger="click"
        placement="bottomRight"
        open={open}
        onOpenChange={onOpenChange}
        content={content}
        title="Thresholds"
      >
        <Button icon={<SettingOutlined />} disabled={!effective}>
          Thresholds
        </Button>
      </Popover>
    </Flex>
  )
}

export default BacklogHealthSettingsPopover
