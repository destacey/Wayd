'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ProjectLifecycleStageDto,
  ProjectLifecycleStageRequest,
} from '@/src/services/wayd-api'
import { useUpdateProjectLifecycleStageMutation } from '@/src/store/features/ppm/project-lifecycles-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface EditProjectLifecycleStageFormProps {
  lifecycleId: string
  stage: ProjectLifecycleStageDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface UpdateProjectLifecycleStageFormValues {
  name: string
  description: string
}

const mapToRequestValues = (
  values: UpdateProjectLifecycleStageFormValues,
): ProjectLifecycleStageRequest => {
  return {
    name: values.name,
    description: values.description,
  } as ProjectLifecycleStageRequest
}

const EditProjectLifecycleStageForm = ({
  lifecycleId,
  stage,
  onFormComplete,
  onFormCancel,
}: EditProjectLifecycleStageFormProps) => {
  const messageApi = useMessage()

  const [updateProjectLifecycleStage] =
    useUpdateProjectLifecycleStageMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdateProjectLifecycleStageFormValues>({
      onSubmit: async (values: UpdateProjectLifecycleStageFormValues, form) => {
          try {
            const request = mapToRequestValues(values)
            const response = await updateProjectLifecycleStage({
              lifecycleId,
              stageId: stage.id,
              ...request,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Stage updated successfully.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                apiError.detail ??
                  'An error occurred while updating the stage. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the stage. Please try again.',
      permission: 'Permissions.ProjectLifecycles.Update',
    })

  useEffect(() => {
    if (!stage) return
    form.setFieldsValue({
      name: stage.name,
      description: stage.description,
    })
  }, [stage, form])

  return (
    <Modal
      title="Edit Stage"
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
        name="update-project-lifecycle-stage-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 32 },
          ]}
        >
          <Input showCount maxLength={32} />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[{ max: 1024 }]}
        >
          <TextArea
            autoSize={{ minRows: 4, maxRows: 6 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditProjectLifecycleStageForm
