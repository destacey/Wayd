'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeChannelDto } from '@/src/services/wayd-api'
import {
  useCreateIntakeChannelMutation,
  useUpdateIntakeChannelMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input

export interface IntakeChannelFormProps {
  /** The channel to edit, or none to add one to the end of the list. */
  channel?: IntakeChannelDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface IntakeChannelFormValues {
  name: string
  description?: string
}

/**
 * Adds a channel to the company's list, or renames and re-describes one.
 */
const IntakeChannelForm = ({
  channel,
  onFormComplete,
  onFormCancel,
}: IntakeChannelFormProps) => {
  const messageApi = useMessage()
  const isEdit = channel !== undefined
  const action = isEdit ? 'updating' : 'adding'

  const [createChannel] = useCreateIntakeChannelMutation()
  const [updateChannel] = useUpdateIntakeChannelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<IntakeChannelFormValues>({
      onSubmit: async (values, form) => {
        try {
          const description = values.description?.trim() || undefined
          if (channel) {
            await updateChannel({
              id: channel.id,
              name: values.name,
              description,
            }).unwrap()
            messageApi.success('Channel updated.')
          } else {
            await createChannel({ name: values.name, description }).unwrap()
            messageApi.success('Channel added.')
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
                `An error occurred while ${action} the channel. Please try again.`,
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: `An error occurred while ${action} the channel. Please try again.`,
      permission: 'Permissions.IntakeRequests.Administer',
    })

  useEffect(() => {
    if (channel) {
      form.setFieldsValue({
        name: channel.name,
        description: channel.description,
      })
    }
  }, [channel, form])

  return (
    <Modal
      title={isEdit ? 'Edit Channel' : 'Add Channel'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Add'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="intake-channel-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra={
            isEdit
              ? 'Every request from this channel shows the new name.'
              : undefined
          }
        >
          <Input showCount maxLength={64} />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[{ max: 512 }]}
          extra="Shown to requesters choosing where their request came from."
        >
          <TextArea autoSize={{ minRows: 3, maxRows: 6 }} maxLength={512} />
        </Item>
        {!isEdit && (
          <p>
            Desks that offer every channel offer it at once. Desks that chose
            their channels offer it only once a Desk Admin adds it.
          </p>
        )}
      </Form>
    </Modal>
  )
}

export default IntakeChannelForm
