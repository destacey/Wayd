'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { HolidayDto } from '@/src/services/wayd-api'
import {
  useAddHolidayMutation,
  useChangeHolidayMutation,
} from '@/src/store/features/organization/holiday-calendars-api'
import {
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  toFormErrors,
  type ApiError,
} from '@/src/utils'
import { DatePicker, Form, Input, Modal } from 'antd'
import { type Dayjs } from 'dayjs'
import { useEffect } from 'react'

const { Item } = Form

export interface HolidayFormProps {
  calendarId: string
  /** The holiday being changed; absent when adding one. */
  holiday?: HolidayDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  date: Dayjs
  name: string
}

/** Adds a holiday to a calendar, or moves or renames one. */
const HolidayForm = ({
  calendarId,
  holiday,
  onFormComplete,
  onFormCancel,
}: HolidayFormProps) => {
  const messageApi = useMessage()
  const [addHoliday] = useAddHolidayMutation()
  const [changeHoliday] = useChangeHolidayMutation()
  const isEdit = holiday !== undefined

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values, form) => {
        try {
          const request = {
            date: toCalendarDate(values.date),
            name: values.name,
          }
          const response = isEdit
            ? await changeHoliday({
                calendarId,
                holidayId: holiday.id,
                request,
              })
            : await addHoliday({ calendarId, request })
          if (response.error) throw response.error
          messageApi.success(isEdit ? 'Holiday updated.' : 'Holiday added.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'An error occurred while saving the holiday.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while saving the holiday.',
      permission: 'Permissions.HolidayCalendars.Update',
    })

  useEffect(() => {
    if (holiday) {
      form.setFieldsValue({
        date: parseCalendarDate(holiday.date),
        name: holiday.name,
      })
    }
  }, [holiday, form])

  return (
    <Modal
      title={isEdit ? 'Edit Holiday' : 'Add Holiday'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Add'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="holiday-form">
        <Item
          label="Date"
          name="date"
          rules={[{ required: true, message: 'Date is required' }]}
        >
          <DatePicker style={{ width: '100%' }} />
        </Item>
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <Input showCount maxLength={128} placeholder="New Year's Day" />
        </Item>
      </Form>
    </Modal>
  )
}

export default HolidayForm
