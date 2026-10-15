'use client'

import { DatePicker, Form, InputNumber, Modal, Select } from 'antd'
import type { Dayjs } from 'dayjs'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { TimeAwayReason } from '@/src/services/wayd-api'
import {
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  type ApiError,
} from '@/src/utils'
import {
  EmployeeTimeAwayEntryDto,
  useChangeEmployeeTimeAwayMutation,
  useGetTimeAwayReasonsQuery,
  useRecordEmployeeTimeAwayMutation,
} from '@/src/store/features/organization/employee-time-away-api'

const { Item: FormItem } = Form

const WHOLE_DAY = 100

interface Props {
  employeeId: string
  /** Absent when recording time away afresh. */
  timeAway?: EmployeeTimeAwayEntryDto
  onFormComplete: () => void
  onFormCancel: () => void
}

export interface TimeAwayFormValues {
  start: Dayjs
  end: Dayjs
  percentAway: number
  reason: TimeAwayReason
}

/** What the form's values ask the API to record. */
export const timeAwayRequestFor = (values: TimeAwayFormValues) => ({
  start: toCalendarDate(values.start),
  end: toCalendarDate(values.end),
  percentAway: values.percentAway,
  reason: values.reason,
})

const TimeAwayForm = ({
  employeeId,
  timeAway,
  onFormComplete,
  onFormCancel,
}: Props) => {
  const messageApi = useMessage()
  const { data: reasons } = useGetTimeAwayReasonsQuery()
  const [recordTimeAway] = useRecordEmployeeTimeAwayMutation()
  const [changeTimeAway] = useChangeEmployeeTimeAwayMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<TimeAwayFormValues>({
      onSubmit: async (values) => {
        try {
          const request = timeAwayRequestFor(values)
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
          messageApi.error(apiError.detail ?? 'Failed to save the time away.')
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to save the time away.',
    })

  const reasonOptions =
    reasons
      ?.slice()
      .sort((a, b) => a.order - b.order)
      .map((r) => ({ label: r.name, value: r.code })) ?? []

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
          start: timeAway ? parseCalendarDate(timeAway.start) : undefined,
          end: timeAway ? parseCalendarDate(timeAway.end) : undefined,
          percentAway: timeAway?.percentAway ?? WHOLE_DAY,
          reason: timeAway?.reason,
        }}
      >
        <FormItem name="start" label="First day" rules={[{ required: true }]}>
          <DatePicker />
        </FormItem>
        <FormItem
          name="end"
          label="Last day"
          dependencies={['start']}
          extra="The same as the first day for a single day."
          rules={[
            { required: true },
            ({ getFieldValue }) => ({
              validator: (_, value: Dayjs | null | undefined) => {
                const start: Dayjs | undefined = getFieldValue('start')
                return !value || !start || !value.isBefore(start, 'day')
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error('The last day cannot be before the first.'),
                    )
              },
            }),
          ]}
        >
          <DatePicker />
        </FormItem>
        <FormItem
          name="percentAway"
          label="Share of each day away"
          extra="100% for whole days, 50% for half days. Away from every team, in proportion to what each has."
          rules={[{ required: true }]}
        >
          <InputNumber min={1} max={WHOLE_DAY} precision={0} suffix="%" />
        </FormItem>
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
