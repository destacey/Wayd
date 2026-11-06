'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useCreateIntakeRequestTagCategoryMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal, Switch } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CreateTagCategoryFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateTagCategoryFormValues {
  name: string
  description?: string
  allowsMany: boolean
}

/** Adds a tag category to the end of the list. */
const CreateTagCategoryForm = ({
  onFormComplete,
  onFormCancel,
}: CreateTagCategoryFormProps) => {
  const messageApi = useMessage()

  const [createTagCategory] = useCreateIntakeRequestTagCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateTagCategoryFormValues>({
      onSubmit: async (values, form) => {
        try {
          await createTagCategory({
            name: values.name,
            description: values.description?.trim() || undefined,
            allowsMany: values.allowsMany ?? false,
          }).unwrap()
          messageApi.success('Tag category added.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while adding the tag category. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding the tag category. Please try again.',
      permission: 'Permissions.IntakeRequests.Administer',
    })

  return (
    <Modal
      title="Add Tag Category"
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
        name="create-intake-request-tag-category-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra="The category requests are grouped by — Area, Customer segment. Must be unique."
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
          // more tags than the category permits, so the edit form does not offer it.
          extra="Whether a request can carry several tags from this category. Cannot be changed later."
        >
          <Switch checkedChildren="Yes" unCheckedChildren="No" />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateTagCategoryForm
