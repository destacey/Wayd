'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDeleteHolidayCalendarMutation } from '@/src/store/features/organization/holiday-calendars-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Modal } from 'antd'
import { useState } from 'react'

export interface DeleteHolidayCalendarFormProps {
  calendar: { id: string; name: string }
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteHolidayCalendarForm = ({
  calendar,
  onFormComplete,
  onFormCancel,
}: DeleteHolidayCalendarFormProps) => {
  const messageApi = useMessage()
  const [deleteCalendar] = useDeleteHolidayCalendarMutation()
  const [isDeleting, setIsDeleting] = useState(false)

  const handleOk = async () => {
    setIsDeleting(true)
    try {
      const response = await deleteCalendar(calendar.id)
      if (response.error) throw response.error
      messageApi.success(`Holiday calendar "${calendar.name}" deleted.`)
      onFormComplete()
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(
        apiError.detail ??
          'An error occurred while deleting the holiday calendar.',
      )
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <Modal
      title="Delete Holiday Calendar"
      open={true}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isDeleting}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      <p>
        Are you sure you want to delete <strong>{calendar.name}</strong> and its
        holidays?
      </p>
      <Alert
        type="warning"
        showIcon
        title="This cannot be undone. A calendar a team operating model uses, or the system default, cannot be deleted."
        style={{ marginTop: 12 }}
      />
    </Modal>
  )
}

export default DeleteHolidayCalendarForm
