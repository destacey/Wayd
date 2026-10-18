'use client'

import { Form, Modal } from 'antd'
import { HolidayCalendarSelect } from '@/src/components/common/scheduling'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { NavigationDto } from '@/src/services/wayd-api'
import { toFormErrors, toApiError } from '@/src/utils'
import { useChangeEmployeeHolidayCalendarMutation } from '@/src/store/features/organization/employee-time-away-api'

const { Item: FormItem } = Form

/** What following no calendar means for a person. */
export const SYSTEM_DEFAULT_LABEL = 'System default'

interface Props {
  employeeId: string
  /** The calendar the person follows now; absent when they follow the system default. */
  holidayCalendar?: NavigationDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  holidayCalendarId?: string
}

const EmployeeHolidayCalendarForm = ({
  employeeId,
  holidayCalendar,
  onFormComplete,
  onFormCancel,
}: Props) => {
  const messageApi = useMessage()
  const [changeHolidayCalendar] = useChangeEmployeeHolidayCalendarMutation()

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await changeHolidayCalendar({
            employeeId,
            request: { holidayCalendarId: values.holidayCalendarId },
          })
          if (response.error) throw response.error
          messageApi.success('Holiday calendar saved.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to save the holiday calendar.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to save the holiday calendar.',
      permission: 'Permissions.Employees.Update',
    })

  return (
    <Modal
      title="Change Holiday Calendar"
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
        name="employee-holiday-calendar-form"
        initialValues={{ holidayCalendarId: holidayCalendar?.id }}
      >
        <FormItem
          name="holidayCalendarId"
          label="Holiday Calendar"
          extra="The shared calendar for where this person works, such as their country's. They take its holidays instead of the system default's, on every team. Leave empty for the system default."
        >
          <HolidayCalendarSelect
            emptyLabel={SYSTEM_DEFAULT_LABEL}
            current={holidayCalendar}
            aria-label="Holiday Calendar"
          />
        </FormItem>
      </Form>
    </Modal>
  )
}

export default EmployeeHolidayCalendarForm
