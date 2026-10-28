'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useCreateIntakeRequestTagCategoryMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal, Switch } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CreateTagAxisFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateTagAxisFormValues {
  name: string
  description?: string
  allowsMany: boolean
}

/** Adds a tag axis to the end of the list. */
const CreateTagAxisForm = ({
  onFormComplete,
  onFormCancel,
}: CreateTagAxisFormProps) => {
  const messageApi = useMessage()

  const [createTagAxis] = useCreateIntakeRequestTagCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateTagAxisFormValues>({
      onSubmit: async (values, form) => {
        try {
          await createTagAxis({
            name: values.name,
            description: values.description?.trim() || undefined,
            allowsMany: values.allowsMany ?? false,
          }).unwrap()
          messageApi.success('Tag axis added.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while adding the tag axis. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding the tag axis. Please try again.',
      permission: 'Permissions.IntakeRequests.Administer',
    })

  return (
    <Modal
      title="Add Tag Axis"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-intake-request-tag-axis-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra="The axis requests are filed along — Area, Customer segment. Must be unique."
        >
          <Input showCount maxLength={64} />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 512 }]}>
          <TextArea
            autoSize={{ minRows: 3, maxRows: 6 }}
            showCount
            maxLength={512}
          />
        </Item>
        <Item
          name="allowsMany"
          label="Allow multiple tags?"
          valuePropName="checked"
          initialValue={false}
          // Fixed once set: narrowing it later would leave requests holding
          // more tags than the axis permits, so the edit form does not offer it.
          extra="Whether a request can carry several tags from this axis. Cannot be changed later."
        >
          <Switch checkedChildren="Yes" unCheckedChildren="No" />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateTagAxisForm
