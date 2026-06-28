'use client'

import { RoadmapColorDto } from '@/src/services/wayd-api'
import { Select, theme } from 'antd'
import { ReactNode } from 'react'

const { useToken } = theme

export interface RoadmapColorPickerProps {
  /** The colors configured on the roadmap. */
  entries: RoadmapColorDto[]
  // The following are supplied by antd Form.Item when used as a custom control.
  id?: string
  value?: string
  onChange?: (value: string | undefined) => void
  /** Notifies when the dropdown opens/closes (used by the grid's keyboard nav). */
  onOpenChange?: (open: boolean) => void
  /** Auto-opens the dropdown on mount (used when entering inline edit in the grid). */
  defaultOpen?: boolean
}

const ColorOptionLabel = ({
  color,
  children,
}: {
  color: string
  children: ReactNode
}) => {
  const { token } = useToken()
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: token.marginXS,
        height: '100%',
        verticalAlign: 'middle',
      }}
    >
      <span
        style={{
          display: 'inline-block',
          width: 14,
          height: 14,
          borderRadius: token.borderRadiusSM,
          backgroundColor: color,
          border: `1px solid ${token.colorBorderSecondary}`,
          flex: 'none',
          verticalAlign: 'middle',
        }}
      />
      <span>{children}</span>
    </span>
  )
}

/**
 * A color selector that constrains the choice to the roadmap's configured colors,
 * presented as a dropdown of labeled swatches. Used in place of the freeform color picker
 * when a roadmap has colors configured. If the current value is not one of the configured
 * colors (e.g. it was set before the roadmap's colors were configured), it is shown as an
 * additional "Custom" option so the existing value stays visible.
 */
const RoadmapColorPicker = ({
  id,
  entries,
  value,
  onChange,
  onOpenChange,
  defaultOpen,
}: RoadmapColorPickerProps) => {
  const hasMatch = entries.some(
    (e) => e.color.toUpperCase() === value?.toUpperCase(),
  )

  const options = entries.map((e) => ({
    value: e.color,
    label: e.name,
  }))

  if (value && !hasMatch) {
    options.push({ value, label: 'Custom' })
  }

  return (
    <Select<string>
      id={id}
      value={value}
      onChange={(next) => onChange?.(next)}
      onOpenChange={onOpenChange}
      defaultOpen={defaultOpen}
      allowClear
      placeholder="Select a color"
      style={{ width: '100%' }}
      options={options}
      optionRender={(option) => (
        <ColorOptionLabel color={String(option.value)}>
          {option.label}
        </ColorOptionLabel>
      )}
      labelRender={(label) =>
        label.value ? (
          <ColorOptionLabel color={String(label.value)}>
            {label.label}
          </ColorOptionLabel>
        ) : (
          label.label
        )
      }
    />
  )
}

export default RoadmapColorPicker
