'use client'

import { Flex, Typography } from 'antd'
import type { DependencyEdgeStyle } from './dependency-map-svg'
import styles from './dependency-map.module.css'

const { Text } = Typography

export interface DependencyMapLegendItem {
  label: string
  style: DependencyEdgeStyle
  /** What the line means, for the reader who hovers it. */
  description?: string
}

/**
 * A key to the map's lines, drawn with the same strokes the edges use, so a colour and a dash are never
 * explained in words that could drift from what is on the canvas.
 */
const DependencyMapLegend = ({
  items,
}: {
  items: DependencyMapLegendItem[]
}) => (
  <Flex wrap gap="small" component="ul" className={styles.legend}>
    {items.map(({ label, style, description }) => (
      <Flex
        key={label}
        component="li"
        align="center"
        gap={4}
        title={description}
      >
        <svg width={24} height={8} aria-hidden>
          <line
            x1={0}
            y1={4}
            x2={24}
            y2={4}
            stroke={style.stroke}
            strokeWidth={style.width}
            strokeDasharray={style.dashed ? '6 4' : undefined}
          />
        </svg>
        <Text type="secondary">{label}</Text>
      </Flex>
    ))}
  </Flex>
)

export default DependencyMapLegend
