'use client'

import { DatePicker, Form, Input, InputNumber, Modal, Typography } from 'antd'
import { CreatePlanningIntervalRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useCreatePlanningIntervalMutation } from '@/src/store/features/planning/planning-interval-api'
import { useModalForm } from '@/src/hooks'
import { type Dayjs } from 'dayjs'

const { Item } = Form
const { TextArea } = Input
const { Text } = Typography

export interface CreatePlanningIntervalFormProps {
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreatePlanningIntervalFormValues {
  name: string
  description?: string
  start: Dayjs
  end: Dayjs
  iterationWeeks: number
  iterationPrefix?: string
}

const mapToRequestValues = (
  values: CreatePlanningIntervalFormValues,
): CreatePlanningIntervalRequest => {
  return {
    name: values.name,
    description: values.description,
    start: values.start?.format('YYYY-MM-DD'),
    end: values.end?.format('YYYY-MM-DD'),
    iterationWeeks: values.iterationWeeks,
    iterationPrefix: values.iterationPrefix,
  } as unknown as CreatePlanningIntervalRequest
}

const CreatePlanningIntervalForm = ({
  onFormCreate,
  onFormCancel,
}: CreatePlanningIntervalFormProps) => {
  const messageApi = useMessage()

  const [createPlanningInterval] = useCreatePlanningIntervalMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreatePlanningIntervalFormValues>({
      onSubmit: async (values: CreatePlanningIntervalFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await createPlanningInterval(request)
          if (response.error) {
            throw response.error
          }
          messageApi.success('Successfully created planning interval.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              'An error occurred while creating the planning interval. Please try again.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the planning interval. Please try again.',
      permission: 'Permissions.PlanningIntervals.Create',
    })

  const formValues = Form.useWatch([], form)

  return (
    <Modal
      title="Create Planning Interval"
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
        name="create-planning-interval-form"
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
          rules={[{ max: 2048 }]}
        >
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={2048}
          />
        </Item>
        <Item label="Start" name="start" rules={[{ required: true }]}>
          <DatePicker />
        </Item>
        <Item label="End" name="end" rules={[{ required: true }]}>
          <DatePicker />
        </Item>
        <Item
          label="Iteration Weeks"
          name="iterationWeeks"
          rules={[{ required: true }]}
        >
          <InputNumber min={1} max={10} />
        </Item>
        <Item
          label="Iteration Prefix"
          name="iterationPrefix"
          extra="Iteration Name Template: Iteration Prefix + Iteration Number"
        >
          <Input />
        </Item>
        <>
          {formValues &&
            formValues.iterationPrefix &&
            formValues.iterationPrefix != null && (
              <Text type="secondary">
                Iteration name format: {formValues.iterationPrefix}1,{' '}
                {formValues.iterationPrefix}2, ...
              </Text>
            )}
        </>
      </Form>
    </Modal>
  )
}

export default CreatePlanningIntervalForm
