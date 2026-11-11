import { FC } from 'react'

export interface WaydStatisticNumberProps {
  value?: number | string
  /** Decimal places to show: extra digits are cut off, missing ones are filled with zeros. */
  precision?: number
}

/**
 * Groups the integer part in thousands. A value that is not a plain decimal number (`1e21`, `NaN`,
 * text) is returned as written.
 */
const formatNumber = (value: number | string, precision?: number): string => {
  const text = String(value)
  const cells = text.match(/^(-?)(\d*)(\.(\d+))?$/)
  if (!cells || text === '-') return text

  const sign = cells[1]
  const int = (cells[2] || '0').replace(/\B(?=(\d{3})+(?!\d))/g, ',')
  let decimal = cells[4] ?? ''
  if (precision !== undefined && !Number.isNaN(precision)) {
    // TODO: round to the precision instead of cutting the extra digits off.
    decimal = decimal.padEnd(precision, '0').slice(0, Math.max(precision, 0))
  }

  return decimal ? `${sign}${int}.${decimal}` : `${sign}${int}`
}

/**
 * A number with thousands separators, for a grid cell or inline text. Renders
 * nothing without a value.
 */
export const WaydStatisticNumber: FC<WaydStatisticNumberProps> = ({
  value,
  precision,
}) => {
  if (value === null || value === undefined) return null

  return <span>{formatNumber(value, precision)}</span>
}

export default WaydStatisticNumber
