'use client'

import { RoadmapColorDto } from '@/src/services/wayd-api'
import { contrastText } from '@/src/components/common/timeline/core/color'
import { Space, Tag, Typography, theme } from 'antd'

const { Text } = Typography
const { useToken } = theme

export interface RoadmapColorLegendProps {
  colors: RoadmapColorDto[]
}

/**
 * A read-only legend describing the meaning of a roadmap's configured colors. Rendered
 * below the timeline when the roadmap has colors configured. Each color is shown as a
 * filled tag, mirroring how the color appears on the timeline bars.
 */
const RoadmapColorLegend = ({ colors }: RoadmapColorLegendProps) => {
  const { token } = useToken()

  if (colors.length === 0) return null

  const ordered = [...colors].sort((a, b) => a.order - b.order)

  return (
    <Space
      wrap
      size={[8, 8]}
      style={{ padding: `${token.paddingSM}px ${token.padding}px` }}
    >
      <Text type="secondary" style={{ fontSize: token.fontSizeSM }}>
        Color Legend:
      </Text>
      {ordered.map((color) => (
        // antd's `color` prop tints custom hex values rather than filling them,
        // so set the fill explicitly to mirror the solid timeline bars.
        <Tag
          key={color.color}
          style={{
            backgroundColor: color.color,
            borderColor: color.color,
            color: contrastText(color.color),
            marginInlineEnd: 0,
          }}
        >
          {color.name}
        </Tag>
      ))}
    </Space>
  )
}

export default RoadmapColorLegend
