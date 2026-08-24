'use client'

import { theme, Typography } from 'antd'

const { Text } = Typography

export interface RecordKeyProps {
  /** The identifier shown. For teams this is the `code`, not the numeric key. */
  value: string
}

/**
 * The record's identifier, rendered as its own element rather than
 * concatenated into the title.
 *
 * Monospace and tabular so keys of differing length align down a page, and
 * selectable on its own so it can be copied into a ticket without dragging
 * through the record name.
 */
const RecordKey = ({ value }: RecordKeyProps) => {
  const { token } = theme.useToken()

  return (
    <Text
      code={false}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        height: 24,
        padding: `0 ${token.paddingXS}px`,
        borderRadius: token.borderRadius,
        background: token.colorFillTertiary,
        border: `1px solid ${token.colorBorderSecondary}`,
        fontFamily: token.fontFamilyCode,
        fontSize: token.fontSizeSM,
        fontVariantNumeric: 'tabular-nums',
        color: token.colorTextSecondary,
        flexShrink: 0,
        whiteSpace: 'nowrap',
      }}
    >
      {value}
    </Text>
  )
}

export default RecordKey
