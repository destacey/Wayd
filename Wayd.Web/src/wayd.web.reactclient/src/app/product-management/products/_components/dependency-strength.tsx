'use client'

import { DependencyStrength } from '@/src/services/wayd-api'
import { Radio, Tag, Tooltip } from 'antd'

/**
 * What each strength means, in the words the forms and the grid both use.
 */
export const dependencyStrengthDescription: Record<DependencyStrength, string> =
  {
    [DependencyStrength.Hard]: 'The product stops working without it.',
    [DependencyStrength.Soft]:
      'The product degrades or loses a feature without it, but keeps working.',
  }

/**
 * A strength as a tag. Hard is the one given a colour: it is the strength that means an outage upstream is
 * an outage here, so it is what a reader scanning the list needs to find.
 */
export const DependencyStrengthTag = ({
  strength,
}: {
  strength: DependencyStrength
}) => (
  <Tooltip title={dependencyStrengthDescription[strength]}>
    <Tag color={strength === DependencyStrength.Hard ? 'volcano' : 'default'}>
      {strength}
    </Tag>
  </Tooltip>
)

export interface DependencyStrengthRadioProps {
  /** Injected by Form.Item. */
  value?: DependencyStrength
  /** Injected by Form.Item. */
  onChange?: (value: DependencyStrength) => void
  id?: string
}

/**
 * Chooses a strength, with no default. Defaulting either way would skew attributing a provider's downtime to
 * its consumers later — hard over-attributes, soft hides real impact — so the choice is always made.
 */
export const DependencyStrengthRadio = ({
  value,
  onChange,
  id,
}: DependencyStrengthRadioProps) => (
  <Radio.Group
    id={id}
    value={value}
    onChange={(e) => onChange?.(e.target.value)}
    options={[DependencyStrength.Hard, DependencyStrength.Soft].map(
      (strength) => ({
        value: strength,
        label: `${strength} — ${dependencyStrengthDescription[strength]}`,
      }),
    )}
    vertical
  />
)
