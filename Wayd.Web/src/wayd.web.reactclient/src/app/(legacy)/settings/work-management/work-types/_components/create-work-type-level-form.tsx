import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { CreateWorkTypeLevelRequest } from '@/src/services/wayd-api'
import { useCreateWorkTypeLevelMutation } from '@/src/store/features/work-management/work-type-level-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CreateWorkTypeLevelFormProps {
  onFormSave: () => void
  onFormCancel: () => void
}

interface CreateWorkTypeLevelFormValues {
  name: string
  description?: string
}

const mapToRequestValues = (
  values: CreateWorkTypeLevelFormValues,
): CreateWorkTypeLevelRequest => {
  return {
    name: values.name,
    description: values.description,
  }
}

const CreateWorkTypeLevelForm = ({
  onFormSave,
  onFormCancel,
}: CreateWorkTypeLevelFormProps) => {
  const messageApi = useMessage()

  const [createWorkTypeLevelMutation] = useCreateWorkTypeLevelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateWorkTypeLevelFormValues>({
      onSubmit: async (values: CreateWorkTypeLevelFormValues, form) => {
          try {
            const request = mapToRequestValues(values)
            const response = await createWorkTypeLevelMutation(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Successfully created work type level.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                'An unexpected error occurred while creating the work type level.',
              )
              console.error(error)
            }
            return false
          }
        },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while creating the work type level.',
      permission: 'Permissions.WorkTypeLevels.Create',
    })

  return (
    <Modal
      title="Create Work Type Level"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-work-type-level-form"
      >
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item
          name="description"
          label="Description"
          initialValue=""
          rules={[{ max: 1024 }]}
        >
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateWorkTypeLevelForm
