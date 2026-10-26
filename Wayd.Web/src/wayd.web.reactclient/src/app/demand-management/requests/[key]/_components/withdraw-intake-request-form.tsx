'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import { useWithdrawIntakeRequestMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { TextArea } = Input
const { Paragraph } = Typography

export interface WithdrawIntakeRequestFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface WithdrawFormValues {
  note?: string
}

/**
 * The requester withdrawing their own request. Offered where the server's `canWithdraw` says so: to the
 * requester, while the request is open.
 */
const WithdrawIntakeRequestForm: FC<WithdrawIntakeRequestFormProps> = ({
  request,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [withdraw] = useWithdrawIntakeRequestMutation()

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<WithdrawFormValues>({
      onSubmit: async (values, form) => {
        try {
          await withdraw({
            requestId: request.summary.id,
            note: values.note?.trim() || undefined,
          }).unwrap()
          messageApi.success('Request withdrawn.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to withdraw the request.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to withdraw the request.',
    })

  return (
    <Modal
      title="Withdraw Request"
      open={isOpen}
      onOk={handleOk}
      okText="Withdraw"
      okButtonProps={{ danger: true }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph>
        Withdrawing tells the {request.summary.desk.name} desk you no longer
        need this. The request stays on record, and the desk can reopen it.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="withdraw-intake-request-form"
      >
        <Item
          name="note"
          label="Note"
          extra="Optional. The desk can read it."
          rules={[
            { max: 1024, message: 'Keep the note under 1024 characters.' },
          ]}
        >
          <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default WithdrawIntakeRequestForm
