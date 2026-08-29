'use client'

import {
  useDeleteEmployeeMutation,
  useGetEmployeeQuery,
} from '@/src/store/features/organizations/employee-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'

export interface DeleteEmployeeFormProps {
  employeeKey: number
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteEmployeeForm = ({
  employeeKey,
  onFormComplete,
  onFormCancel,
}: DeleteEmployeeFormProps) => {
  const messageApi = useMessage()

  const { data: employeeData } = useGetEmployeeQuery(employeeKey)

  const [deleteEmployeeMutation] = useDeleteEmployeeMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      if (!employeeData) return false
      try {
        await deleteEmployeeMutation(employeeData.id)
        messageApi.success('Successfully deleted Employee.')
        return true
      } catch (error) {
        messageApi.error(
          'An unexpected error occurred while deleting the employee.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the employee.',
    permission: 'Permissions.Employees.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Employee?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      okButtonProps={{ disabled: !employeeData }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {employeeData?.key} - {employeeData?.displayName}
    </Modal>
  )
}

export default DeleteEmployeeForm
