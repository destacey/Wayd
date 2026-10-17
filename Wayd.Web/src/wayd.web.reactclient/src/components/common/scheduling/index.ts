export { default as TimeZoneSelect, timeZoneLabel } from './time-zone-select'
export type { TimeZoneSelectProps } from './time-zone-select'
export {
  default as WorkingDaysSelect,
  daysOfWeek,
  formatWorkingDays,
} from './working-days-select'
export type { WorkingDaysSelectProps } from './working-days-select'
export { default as HolidayCalendarSelect } from './holiday-calendar-select'
export type { HolidayCalendarSelectProps } from './holiday-calendar-select'
export {
  default as ReportingCadenceFields,
  toReportingCadenceRequest,
  formatReportingCadence,
  MIN_CADENCE_WEEKS,
  MAX_CADENCE_WEEKS,
} from './reporting-cadence-fields'
export type { ReportingCadenceFormValues } from './reporting-cadence-fields'

// The server's bound (SchedulingSettingsValidator.MaxCommitmentGraceDays), which rejects anything past it.
export const MAX_COMMITMENT_GRACE_DAYS = 14

/** The form rule for a working week: at least one day, as the server requires. */
export const workingDaysRule = {
  validator: (_: unknown, value: unknown[] | undefined) =>
    value && value.length > 0
      ? Promise.resolve()
      : Promise.reject(new Error('Choose at least one working day')),
}
