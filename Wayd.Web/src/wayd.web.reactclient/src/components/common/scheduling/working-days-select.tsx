'use client'

import { Checkbox } from 'antd'
import { IsoDayOfWeek } from '@/src/services/wayd-api'

/** The days of the week, Monday first, as the server orders a working week. */
export const daysOfWeek: IsoDayOfWeek[] = [
  IsoDayOfWeek.Monday,
  IsoDayOfWeek.Tuesday,
  IsoDayOfWeek.Wednesday,
  IsoDayOfWeek.Thursday,
  IsoDayOfWeek.Friday,
  IsoDayOfWeek.Saturday,
  IsoDayOfWeek.Sunday,
]

const shortName = (day: IsoDayOfWeek) => day.slice(0, 3)

/**
 * A working week for display: a run of consecutive days as a range
 * ("Mon–Fri"), anything else as a list in week order ("Mon, Wed, Sun").
 */
export const formatWorkingDays = (days: IsoDayOfWeek[] | undefined) => {
  const ordered = daysOfWeek.filter((d) => days?.includes(d))
  if (ordered.length === 0) return ''
  if (ordered.length === daysOfWeek.length) return 'Every day'

  const first = daysOfWeek.indexOf(ordered[0])
  const consecutive = ordered.every(
    (d, i) => daysOfWeek.indexOf(d) === first + i,
  )
  return consecutive && ordered.length > 2
    ? `${shortName(ordered[0])}–${shortName(ordered[ordered.length - 1])}`
    : ordered.map(shortName).join(', ')
}

export interface WorkingDaysSelectProps {
  value?: IsoDayOfWeek[]
  onChange?: (days: IsoDayOfWeek[]) => void
  disabled?: boolean
}

/** Checkboxes for the days of the week a team works, kept in week order. */
const WorkingDaysSelect = ({
  value,
  onChange,
  disabled,
}: WorkingDaysSelectProps) => (
  <Checkbox.Group
    value={value}
    disabled={disabled}
    onChange={(days) => onChange?.(daysOfWeek.filter((d) => days.includes(d)))}
    options={daysOfWeek.map((day) => ({
      value: day,
      label: shortName(day),
    }))}
  />
)

export default WorkingDaysSelect
