'use client'

import { DatePicker, Form, InputNumber, Modal, Segmented, Select } from 'antd'
import type { Dayjs } from 'dayjs'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { TimeAwayReason } from '@/src/services/wayd-api'
import {
  CalendarDate,
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  toFormErrors,
  type ApiError,
} from '@/src/utils'
import {
  EmployeeTimeAwayEntryDto,
  useChangeEmployeeTimeAwayMutation,
  useGetTimeAwayReasonsQuery,
  useRecordEmployeeTimeAwayMutation,
} from '@/src/store/features/organization/employee-time-away-api'
import {
  durationOf,
  percentAwayFor,
  TimeAwayDuration,
} from './time-away-duration'

const { Item: FormItem } = Form
const { RangePicker } = DatePicker

interface Props {
  employeeId: string
  /** How many hours a working day has, which hours entered are a share of. */
  workingDayHours: number
  /** Absent when recording time away afresh. */
  timeAway?: EmployeeTimeAwayEntryDto
  /** The days a new stretch starts on, such as a sprint's. */
  initialDates?: { start: CalendarDate; end: CalendarDate }
  onFormComplete: () => void
  onFormCancel: () => void
}

export interface TimeAwayFormValues {
  /** The first and last days away; the same day twice for a single day. */
  dates: [Dayjs, Dayjs]
  duration: TimeAwayDuration
  /** The hours away each day, when the duration is in hours. */
  hours?: number
  reason: TimeAwayReason
}

/** What the form's values ask the API to record: each day's share as a percent. */
export const timeAwayRequestFor = (
  values: TimeAwayFormValues,
  workingDayHours: number,
) => ({
  start: toCalendarDate(values.dates[0]),
  end: toCalendarDate(values.dates[1]),
  percentAway: percentAwayFor(values.duration, values.hours, workingDayHours),
  reason: values.reason,
})

/**
 * Shows the server's errors on the fields the person filled in: `start` and
 * `end` on the range picker, and `percentAway` on whichever field set it.
 */
export const fieldForError =
  (duration: TimeAwayDuration) =>
  (key: string): string | undefined => {
    if (key === 'start' || key === 'end') return 'dates'
    if (key === 'percentAway') return duration === 'hours' ? 'hours' : 'duration'
    return undefined
  }

const TimeAwayForm = ({
  employeeId,
  workingDayHours,
  timeAway,
  initialDates,
  onFormComplete,
  onFormCancel,
}: Props) => {
  const messageApi = useMessage()
  const { data: reasons } = useGetTimeAwayReasonsQuery()
  const [recordTimeAway] = useRecordEmployeeTimeAwayMutation()
  const [changeTimeAway] = useChangeEmployeeTimeAwayMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<TimeAwayFormValues>({
      onSubmit: async (values, form) => {
        try {
          const request = timeAwayRequestFor(values, workingDayHours)
          const response = timeAway
            ? await changeTimeAway({
                employeeId,
                timeAwayId: timeAway.id,
                request,
              })
            : await recordTimeAway({ employeeId, request })
          if (response.error) throw response.error
          messageApi.success('Time away saved.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(
              toFormErrors(apiError.errors, {
                fieldFor: fieldForError(values.duration),
              }),
            )
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to save the time away.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to save the time away.',
    })

  const duration = Form.useWatch('duration', form)

  const reasonOptions =
    reasons
      ?.slice()
      .sort((a, b) => a.order - b.order)
      .map((r) => ({ label: r.name, value: r.code })) ?? []

  const recorded = timeAway
    ? durationOf(timeAway.percentAway, workingDayHours)
    : { duration: 'full' as const }
  const start = timeAway?.start ?? initialDates?.start
  const end = timeAway?.end ?? initialDates?.end

  return (
    <Modal
      title={timeAway ? 'Change Time Away' : 'Record Time Away'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="time-away-form"
        initialValues={{
          dates:
            start && end
              ? [parseCalendarDate(start), parseCalendarDate(end)]
              : undefined,
          duration: recorded.duration,
          hours: recorded.hours,
          reason: timeAway?.reason,
        }}
      >
        <FormItem
          name="dates"
          label="Date Range"
          extra="Pick the same day twice for a single day."
          rules={[
            { required: true, message: 'Select the first and last days.' },
          ]}
        >
          <RangePicker />
        </FormItem>
        <FormItem
          name="duration"
          label="Away for"
          extra="Each day of the stretch. Away from every team the person is on, in proportion to what each has."
          rules={[{ required: true }]}
        >
          <Segmented<TimeAwayDuration>
            options={[
              { label: 'Full day', value: 'full' },
              { label: 'Half day', value: 'half' },
              { label: 'Hours', value: 'hours' },
            ]}
          />
        </FormItem>
        {duration === 'hours' && (
          <FormItem
            name="hours"
            label="Hours away each day"
            extra={`Of the ${workingDayHours}-hour working day.`}
            rules={[{ required: true, message: 'Enter the hours away.' }]}
          >
            <InputNumber
              min={0.25}
              max={workingDayHours}
              step={0.25}
              precision={2}
              suffix="h"
            />
          </FormItem>
        )}
        <FormItem
          name="reason"
          label="Reason"
          extra="Shown only to the person, the people they report to and time away administrators. Everyone else sees that they are away, not why."
          rules={[{ required: true }]}
        >
          <Select placeholder="Select a reason" options={reasonOptions} />
        </FormItem>
      </Form>
    </Modal>
  )
}

export default TimeAwayForm
