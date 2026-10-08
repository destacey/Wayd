'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ObjectIdAndKey } from '@/src/services/wayd-api'
import {
  useCreateHolidayCalendarMutation,
  useUpdateHolidayCalendarMutation,
} from '@/src/store/features/organization/holiday-calendars-api'
import { isApiError, toFormErrors, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useEffect, useRef } from 'react'

const { Item } = Form
const { TextArea } = Input

/** The calendar being edited; absent when creating one. */
export interface EditedHolidayCalendar {
  id: string
  name: string
  description?: string
}

export interface HolidayCalendarFormProps {
  calendar?: EditedHolidayCalendar
  onFormComplete: (created?: ObjectIdAndKey) => void
  onFormCancel: () => void
}

interface FormValues {
  name: string
  description?: string
}

/** Creates a holiday calendar, or renames one and changes its description. */
const HolidayCalendarForm = ({
  calendar,
  onFormComplete,
  onFormCancel,
}: HolidayCalendarFormProps) => {
  const messageApi = useMessage()
  const [createCalendar] = useCreateHolidayCalendarMutation()
  const [updateCalendar] = useUpdateHolidayCalendarMutation()
  const isEdit = calendar !== undefined
  const created = useRef<ObjectIdAndKey | undefined>(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values, form) => {
        try {
          const description = values.description || undefined
          if (isEdit) {
            const response = await updateCalendar({
              id: calendar.id,
              name: values.name,
              description,
            })
            if (response.error) throw response.error
            messageApi.success('Holiday calendar updated.')
          } else {
            const response = await createCalendar({
              name: values.name,
              description,
            })
            if (response.error) throw response.error
            created.current = response.data
            messageApi.success('Holiday calendar created.')
          }
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while saving the holiday calendar.',
            )
          }
          return false
        }
      },
      onComplete: () => onFormComplete(created.current),
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while saving the holiday calendar.',
      permission: isEdit
        ? 'Permissions.HolidayCalendars.Update'
        : 'Permissions.HolidayCalendars.Create',
    })

  useEffect(() => {
    if (calendar) {
      form.setFieldsValue({
        name: calendar.name,
        description: calendar.description,
      })
    }
  }, [calendar, form])

  return (
    <Modal
      title={isEdit ? 'Edit Holiday Calendar' : 'Create Holiday Calendar'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Create'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="holiday-calendar-form"
      >
        <Item
          label="Name"
          name="name"
          extra="Such as a country or office, e.g. United States."
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <Input showCount maxLength={128} />
        </Item>
        <Item label="Description" name="description" rules={[{ max: 1024 }]}>
          <TextArea showCount maxLength={1024} rows={3} />
        </Item>
      </Form>
    </Modal>
  )
}

export default HolidayCalendarForm
