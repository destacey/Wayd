'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { toApiError } from '@/src/utils'
import { Modal } from 'antd'
import { ReactNode } from 'react'

export interface KpiConfirmFormProps {
  title: string
  children: ReactNode
  okText: string
  danger?: boolean
  /** The change to make on OK; it throws on failure, as an unwrapped mutation does. */
  onConfirm: () => Promise<unknown>
  successMessage: string
  failureMessage: string
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * A confirmation for a KPI change that takes no input: closing, reopening or
 * removing a KPI, or removing a measurement. The server's reason is shown when
 * it refuses.
 */
const KpiConfirmForm = ({
  title,
  children,
  okText,
  danger = false,
  onConfirm,
  successMessage,
  failureMessage,
  onFormComplete,
  onFormCancel,
}: KpiConfirmFormProps) => {
  const messageApi = useMessage()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await onConfirm()
        messageApi.success(successMessage)
        return true
      } catch (error) {
        messageApi.error(toApiError(error).detail ?? failureMessage)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: failureMessage,
  })

  return (
    <Modal
      title={title}
      open={isOpen}
      onOk={handleOk}
      okText={okText}
      okType={danger ? 'danger' : 'primary'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {children}
    </Modal>
  )
}

export default KpiConfirmForm
