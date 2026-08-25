'use client'

import { Tag } from 'antd'

export interface RiskExposureTagProps {
  /** The exposure's name as the server sends it, e.g. "High". */
  exposure?: string
}

/**
 * A risk's exposure, coloured by severity.
 *
 * The severity signal a risk matrix would give, without the matrix — no
 * product plots a single risk on a 5x5 grid on its own page, because a grid
 * with one point on it says no more than the label does.
 *
 * Exposure arrives as a name rather than an enum, so an unrecognised value
 * still renders — uncoloured rather than not at all.
 */
const EXPOSURE_COLORS: Record<string, string> = {
  high: 'error',
  medium: 'warning',
  low: 'success',
}

const RiskExposureTag = ({ exposure }: RiskExposureTagProps) => {
  if (!exposure) return null

  return (
    <Tag color={EXPOSURE_COLORS[exposure.toLowerCase()]} style={{ margin: 0 }}>
      {exposure}
    </Tag>
  )
}

export default RiskExposureTag
