import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { UpdateWorkTypeLevelRequest } from '@/src/services/wayd-api'
import {
  useGetWorkTypeLevelQuery,
  useUpdateWorkTypeLevelMutation,
} from '@/src/store/features/work-management/work-type-level-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface EditWorkTypeLevelFormProps {
  levelId: number
  onFormSave: () => void
  onFormCancel: () => void
}

interface EditWorkTypeLevelFormValues {
  name: string
  description?: string
}

const mapToRequestValues = (
  levelId: number,
  values: EditWorkTypeLevelFormValues,
): UpdateWorkTypeLevelRequest => {
  return {
    id: levelId,
    name: values.name,
    description: values.description,
  }
}

const EditWorkTypeLevelForm = ({
  levelId,
  onFormSave,
  onFormCancel,
}: EditWorkTypeLevelFormProps) => {
  const messageApi = useMessage()

  const { data: workTypeLevelData } = useGetWorkTypeLevelQuery(levelId)
  const [updateLevelMutation] = useUpdateWorkTypeLevelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditWorkTypeLevelFormValues>({
      onSubmit: async (values: EditWorkTypeLevelFormValues, form) => {
          try {
            const request = mapToRequestValues(levelId, values)
            const response = await updateLevelMutation(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Successfully updated work type level.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                'An unexpected error occurred while updating the work type level.',
              )
              console.error(error)
            }
            return false
          }
        },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while updating the work type level.',
      permission: 'Permissions.WorkTypeLevels.Update',
    })

  useEffect(() => {
    if (!workTypeLevelData) return
    form.setFieldsValue({
      name: workTypeLevelData.name,
      description: workTypeLevelData.description || '',
    })
  }, [workTypeLevelData, form])

  return (
    <Modal
      title="Edit Work Type Level"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-work-type-level-form"
      >
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <MarkdownEditor maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditWorkTypeLevelForm
