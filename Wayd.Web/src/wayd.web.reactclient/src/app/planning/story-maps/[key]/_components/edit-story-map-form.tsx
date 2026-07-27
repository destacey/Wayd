'use client'

import { Form, Input, Modal } from 'antd'
import { UpdateStoryMapRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useGetStoryMapQuery,
  useUpdateStoryMapMutation,
} from '@/src/store/features/planning/story-maps-api'
import { useModalForm } from '@/src/hooks'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input

export interface EditStoryMapFormProps {
  storyMapKey: string
  onFormUpdate: () => void
  onFormCancel: () => void
}

interface EditStoryMapFormValues {
  name: string
  description?: string
}

const EditStoryMapForm = ({
  storyMapKey,
  onFormUpdate,
  onFormCancel,
}: EditStoryMapFormProps) => {
  const messageApi = useMessage()

  const { data: storyMap } = useGetStoryMapQuery(storyMapKey)
  const [updateStoryMap] = useUpdateStoryMapMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditStoryMapFormValues>({
      onSubmit: async (values: EditStoryMapFormValues, form) => {
        if (!storyMap) return false
        try {
          const request: UpdateStoryMapRequest = {
            name: values.name,
            description: values.description,
          }
          const response = await updateStoryMap({ id: storyMap.id, request })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Successfully updated story map.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              'An error occurred while updating the story map. Please try again.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormUpdate,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the story map. Please try again.',
      permission: 'Permissions.StoryMaps.Update',
    })

  useEffect(() => {
    if (storyMap) {
      form.setFieldsValue({
        name: storyMap.name,
        description: storyMap.description,
      })
    }
  }, [storyMap, form])

  return (
    <Modal
      title="Edit Story Map"
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
        name="edit-story-map-form"
      >
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item label="Description" name="description">
          <TextArea
            autoSize={{ minRows: 2, maxRows: 6 }}
            showCount
            maxLength={2048}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditStoryMapForm
