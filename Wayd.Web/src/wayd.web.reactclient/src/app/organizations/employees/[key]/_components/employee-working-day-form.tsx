'use client'

import { Form, InputNumber, Modal } from 'antd'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { toFormErrors, toApiError } from '@/src/utils'
import { useChangeEmployeeWorkingDayHoursMutation } from '@/src/store/features/organization/employee-time-away-api'
import { formatHours } from '@/src/app/organizations/_components/time-away/time-away-duration'

const { Item: FormItem } = Form

interface Props {
  employeeId: string
  /** The hours of the person's own working day; absent when they have the system's. */
  ownWorkingDayHours?: number
  systemWorkingDayHours: number
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  workingDayHours?: number | null
}

/** How a person's working day reads: their own hours, or the system's. */
export const workingDayLabel = (
  ownWorkingDayHours: number | undefined,
  systemWorkingDayHours: number,
): string =>
  ownWorkingDayHours == null
    ? `System default (${formatHours(systemWorkingDayHours)} h)`
    : `${formatHours(ownWorkingDayHours)} h`

const EmployeeWorkingDayForm = ({
  employeeId,
  ownWorkingDayHours,
  systemWorkingDayHours,
  onFormComplete,
  onFormCancel,
}: Props) => {
  const messageApi = useMessage()
  const [changeWorkingDayHours] = useChangeEmployeeWorkingDayHoursMutation()

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await changeWorkingDayHours({
            employeeId,
            request: {
              workingDayHours: values.workingDayHours ?? undefined,
            },
          })
          if (response.error) throw response.error
          messageApi.success('Working day saved.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to save the working day.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to save the working day.',
      permission: 'Permissions.Employees.Update',
    })

  return (
    <Modal
      title="Change Working Day"
      open={isOpen}
      onOk={handleOk}
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
        name="employee-working-day-form"
        initialValues={{ workingDayHours: ownWorkingDayHours }}
      >
        <FormItem
          name="workingDayHours"
          label="Working day hours"
          extra={`For someone whose working day is shorter or longer than the system's ${formatHours(systemWorkingDayHours)} hours, such as a part-timer. Their allocation and time away are shares of this day on every team, so a day half the system's, allocated in full, adds half a person-day of capacity. Leave empty for the system default.`}
        >
          <InputNumber
            min={1}
            max={24}
            step={0.5}
            precision={2}
            suffix="h"
            placeholder={formatHours(systemWorkingDayHours)}
            aria-label="Working day hours"
          />
        </FormItem>
      </Form>
    </Modal>
  )
}

export default EmployeeWorkingDayForm
