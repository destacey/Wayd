'use client'

// timeline/render/axis.tsx — two-tier timescale header (upper: month/year,
// lower: week/day), span-aware via scale.tiers(). Segments are positioned and
// sized in pixels from the scale.

import { FC } from 'react'
import type { TimeScale } from '../core/scale'
import styles from './timeline.module.css'

export interface AxisProps {
  scale: TimeScale
  height: number
}

export const Axis: FC<AxisProps> = ({ scale, height }) => {
  const { upper, lower } = scale.tiers()
  const tierHeight = height / 2

  return (
    <div className={styles.axis} style={{ height, width: scale.width }}>
      <div className={styles.axisTier} style={{ height: tierHeight }}>
        {upper.map((seg) => {
          const left = scale.toX(seg.startMs)
          const width = scale.toX(seg.endMs) - left
          return (
            <div
              key={`u-${seg.startMs}`}
              className={styles.axisCell}
              style={{ left, width }}
              title={seg.label}
            >
              <span className={styles.axisCellLabel}>{seg.label}</span>
            </div>
          )
        })}
      </div>
      <div
        className={styles.axisTier}
        style={{ height: tierHeight, top: tierHeight }}
      >
        {lower.map((seg) => {
          const left = scale.toX(seg.startMs)
          const width = scale.toX(seg.endMs) - left
          return (
            <div
              key={`l-${seg.startMs}`}
              className={styles.axisCell}
              style={{ left, width }}
            >
              <span className={styles.axisCellLabel}>{seg.label}</span>
            </div>
          )
        })}
      </div>
    </div>
  )
}
