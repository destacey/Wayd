'use client'

import { Form, Input, Modal, Switch } from 'antd'
import { useEffect } from 'react'
import { UpdatePlanningIntervalRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useGetPlanningIntervalQuery,
  useUpdatePlanningIntervalMutation,
} from '@/src/store/features/planning/planning-interval-api'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface EditPlanningIntervalFormProps {
  planningIntervalKey: number
  onFormUpdate: () => void
  onFormCancel: () => void
}

interface EditPlanningIntervalFormValues {
  id: string
  name: string
  description?: string
  objectivesLocked: boolean
}

const mapToRequestValues = (values: EditPlanningIntervalFormValues) => {
  return {
    id: values.id,
    name: values.name,
    description: values.description,
    objectivesLocked: values.objectivesLocked,
  } as UpdatePlanningIntervalRequest
}

const EditPlanningIntervalForm = ({
  planningIntervalKey,
  onFormUpdate,
  onFormCancel,
}: EditPlanningIntervalFormProps) => {
  const messageApi = useMessage()

  const { data: planningIntervalData } =
    useGetPlanningIntervalQuery(planningIntervalKey)
  const [updatePlanningInterval] = useUpdatePlanningIntervalMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditPlanningIntervalFormValues>({
      onSubmit: async (values: EditPlanningIntervalFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await updatePlanningInterval({
            request,
            cacheKey: planningIntervalKey,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Planning interval updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              'An error occurred while updating the planning interval.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormUpdate,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the planning interval. Please try again.',
      permission: 'Permissions.PlanningIntervals.Update',
    })

  useEffect(() => {
    if (!planningIntervalData) return
    form.setFieldsValue({
      id: planningIntervalData.id,
      name: planningIntervalData.name,
      description: planningIntervalData.description || '',
      objectivesLocked: planningIntervalData.objectivesLocked,
    })
  }, [planningIntervalData, form])

  return (
    <Modal
      title="Edit Planning Interval"
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
        name="edit-planning-interval-form"
      >
        <Item name="id" hidden={true}>
          <Input />
        </Item>
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 2048 }]}>
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={2048}
          />
        </Item>
        <Item
          label="Objectives Locked?"
          name="objectivesLocked"
          valuePropName="checked"
        >
          <Switch checkedChildren="Yes" unCheckedChildren="No" />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditPlanningIntervalForm
