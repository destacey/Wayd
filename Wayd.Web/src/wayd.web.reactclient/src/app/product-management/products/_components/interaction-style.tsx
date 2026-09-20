'use client'

import { InteractionStyle } from '@/src/services/wayd-api'
import { Checkbox, Space, Tag, Tooltip, Typography } from 'antd'

const { Text } = Typography

/**
 * What each style means, in the words the forms and the grid both use.
 */
export const interactionStyleDescription: Record<InteractionStyle, string> = {
  [InteractionStyle.Synchronous]:
    'The product waits for an answer, so the one it depends on has to be up at that moment.',
  [InteractionStyle.Asynchronous]:
    'The product sends or receives messages and carries on, so an outage becomes delay rather than failure.',
}

/** Declaration order, which is the order the API returns them in. */
const allStyles = [
  InteractionStyle.Synchronous,
  InteractionStyle.Asynchronous,
] as const

/**
 * The styles as tags, or an explicit note where none were recorded.
 *
 * Synchronous is the one given a colour: paired with a hard dependency it is what caps this product's
 * availability at the other's, so it is what a reader scanning the list needs to find. "Not recorded" is
 * shown rather than nothing, because an empty cell reads as "there are none" — and a reader who assumes
 * that has assumed the dependency is safe.
 */
export const InteractionStyleTags = ({
  styles,
}: {
  styles?: InteractionStyle[]
}) => {
  if (!styles?.length) {
    return (
      <Tooltip title="Nobody has recorded how these products talk. This is not the same as there being no interaction.">
        <Text type="secondary">Not recorded</Text>
      </Tooltip>
    )
  }

  return (
    <Space size={4} wrap>
      {allStyles
        .filter((style) => styles.includes(style))
        .map((style) => (
          <Tooltip key={style} title={interactionStyleDescription[style]}>
            <Tag
              color={style === InteractionStyle.Synchronous ? 'gold' : 'blue'}
            >
              {style}
            </Tag>
          </Tooltip>
        ))}
    </Space>
  )
}

export interface InteractionStyleCheckboxesProps {
  /** Injected by Form.Item. */
  value?: InteractionStyle[]
  /** Injected by Form.Item. */
  onChange?: (value: InteractionStyle[]) => void
  id?: string
  disabled?: boolean
}

/**
 * Chooses any number of styles, since a pair commonly both calls and subscribes.
 *
 * Ticking nothing records nothing, which is not the same as recording that there are none — the API takes
 * an empty list as "not recorded" for exactly that reason, so there is no state here that asserts absence.
 */
export const InteractionStyleCheckboxes = ({
  value,
  onChange,
  id,
  disabled,
}: InteractionStyleCheckboxesProps) => (
  // Wrapped so the Form.Item label keeps something to point at: Checkbox.Group takes no id of its own.
  <div id={id}>
    <Checkbox.Group
      value={value}
      onChange={(checked) => onChange?.(checked as InteractionStyle[])}
      disabled={disabled}
      options={allStyles.map((style) => ({
        value: style,
        label: `${style} — ${interactionStyleDescription[style]}`,
      }))}
    />
  </div>
)
