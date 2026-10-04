import { Dayjs } from 'dayjs'

/**
 * For a DatePicker's `disabledTime`: on the day of `latest`, the hours and
 * minutes after it. `maxDate` alone only disables the later days, so a later
 * time on that day stays pickable.
 */
export const disabledTimeAfter =
  (latest: Dayjs) => (current: Dayjs | null | undefined) => {
    if (!current || !current.isSame(latest, 'day')) return {}

    const range = (from: number, to: number) =>
      Array.from({ length: Math.max(0, to - from) }, (_, i) => from + i)

    return {
      disabledHours: () => range(latest.hour() + 1, 24),
      disabledMinutes: (hour: number) =>
        hour === latest.hour() ? range(latest.minute() + 1, 60) : [],
    }
  }
