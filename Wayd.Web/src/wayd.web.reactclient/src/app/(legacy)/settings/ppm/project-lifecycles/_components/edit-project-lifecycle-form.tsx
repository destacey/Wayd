'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { UpdateProjectLifecycleRequest } from '@/src/services/wayd-api'
import {
  useGetProjectLifecycleQuery,
  useUpdateProjectLifecycleMutation,
} from '@/src/store/features/ppm/project-lifecycles-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface EditProjectLifecycleFormProps {
  lifecycleId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface UpdateProjectLifecycleFormValues {
  name: string
  description: string
}

const mapToRequestValues = (
  values: UpdateProjectLifecycleFormValues,
): UpdateProjectLifecycleRequest => {
  return {
    name: values.name,
    description: values.description,
  } as UpdateProjectLifecycleRequest
}

const EditProjectLifecycleForm = ({
  lifecycleId,
  onFormComplete,
  onFormCancel,
}: EditProjectLifecycleFormProps) => {
  const messageApi = useMessage()

  const { data: lifecycleData, error } =
    useGetProjectLifecycleQuery(lifecycleId)

  const [updateProjectLifecycle] = useUpdateProjectLifecycleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdateProjectLifecycleFormValues>({
      onSubmit: async (values: UpdateProjectLifecycleFormValues, form) => {
          try {
            const request = mapToRequestValues(values)
            const response = await updateProjectLifecycle({
              id: lifecycleData!.id,
              ...request,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Project Lifecycle updated successfully.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                (isApiError(apiError) ? apiError.detail : undefined) ??
                  'An error occurred while updating the project lifecycle. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the project lifecycle. Please try again.',
      permission: 'Permissions.ProjectLifecycles.Update',
    })

  useEffect(() => {
    if (!lifecycleData) return
    form.setFieldsValue({
      name: lifecycleData.name,
      description: lifecycleData.description,
    })
  }, [lifecycleData, form])

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the project lifecycle. Please try again.',
      )
    }
  }, [error, messageApi])

  return (
    <Modal
      title="Edit Project Lifecycle"
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
        name="update-project-lifecycle-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[
            { required: true, message: 'Description is required' },
            { max: 1024 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 6, maxRows: 8 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditProjectLifecycleForm
