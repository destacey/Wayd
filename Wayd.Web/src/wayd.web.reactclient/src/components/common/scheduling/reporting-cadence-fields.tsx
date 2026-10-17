'use client'

import { DatePicker, Form, InputNumber } from 'antd'
import { type Dayjs } from 'dayjs'
import { Methodology } from '@/src/services/wayd-api'
import { formatCalendarDate, toCalendarDate } from '@/src/utils/calendar-date'

const { Item: FormItem } = Form

// The server's bounds (ReportingCadence.MinWeeks/MaxWeeks), which reject anything outside them.
export const MIN_CADENCE_WEEKS = 1
export const MAX_CADENCE_WEEKS = 8

/** The form values the cadence fields own. */
export interface ReportingCadenceFormValues {
  cadenceWeeks?: number | null
  cadenceAnchor?: Dayjs | null
}

/**
 * A cadence for display, "Every 2 weeks" or "Every 2 weeks from Oct 5, 2026", or empty for none.
 */
export const formatReportingCadence = (
  weeks: number | undefined | null,
  anchor?: string | null,
): string => {
  if (!weeks) return ''
  const every = weeks === 1 ? 'Every week' : `Every ${weeks} weeks`
  return anchor ? `${every} from ${formatCalendarDate(anchor)}` : every
}

/**
 * The request's cadence fields from the form's: none unless the team is Kanban and a length is given.
 */
export const toReportingCadenceRequest = (
  methodology: Methodology | undefined,
  values: ReportingCadenceFormValues,
): { cadenceWeeks?: number; cadenceAnchor?: string } =>
  methodology === Methodology.Kanban &&
  values.cadenceWeeks &&
  values.cadenceAnchor
    ? {
        cadenceWeeks: values.cadenceWeeks,
        cadenceAnchor: toCalendarDate(values.cadenceAnchor),
      }
    : {}

/**
 * The cadence length and anchor of a Kanban team's operating model, inside a form that has a `methodology`
 * field. Renders nothing for Scrum, whose sprints are its cadence.
 */
const ReportingCadenceFields = () => {
  const form = Form.useFormInstance()
  const methodology: Methodology | undefined = Form.useWatch(
    'methodology',
    form,
  )
  // preserve: the field registers only once Kanban is chosen, and without it a
  // value set before then reads as undefined.
  const cadenceWeeks: number | null | undefined = Form.useWatch(
    'cadenceWeeks',
    {
      form,
      preserve: true,
    },
  )

  if (methodology !== Methodology.Kanban) return null

  return (
    <>
      <FormItem
        name="cadenceWeeks"
        label="Cadence (weeks)"
        extra="The length of the periods the team's velocity is reported in. Leave empty for no cadence. A cadence is for reporting only: it has no commitment, start or complete."
      >
        <InputNumber
          min={MIN_CADENCE_WEEKS}
          max={MAX_CADENCE_WEEKS}
          precision={0}
          aria-label="Cadence (weeks)"
        />
      </FormItem>
      {cadenceWeeks ? (
        <FormItem
          name="cadenceAnchor"
          label="Cadence Anchor"
          extra="The first day of one of the periods. Every period starts on this day of the week."
          rules={[{ required: true, message: 'Cadence anchor is required' }]}
        >
          <DatePicker style={{ width: '100%' }} aria-label="Cadence Anchor" />
        </FormItem>
      ) : null}
    </>
  )
}

export default ReportingCadenceFields
