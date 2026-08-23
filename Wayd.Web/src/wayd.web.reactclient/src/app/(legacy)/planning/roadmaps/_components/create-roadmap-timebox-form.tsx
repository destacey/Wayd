'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CreateRoadmapTimeboxRequest } from '@/src/services/wayd-api'
import {
  useCreateRoadmapItemMutation,
  useGetRoadmapActivitiesQuery,
} from '@/src/store/features/planning/roadmaps-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Form, Input, Modal, TreeSelect } from 'antd'
import { useEffect } from 'react'
import {
  findParentActivityRange,
  getParentExpansionHint,
} from './roadmap-parent-date-hint'

const { Item } = Form
const { TextArea } = Input
const { RangePicker } = DatePicker

export interface CreateRoadmapTimeboxFormProps {
  roadmapId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateRoadmapTimeboxFormValues {
  parentId?: string
  name: string
  description?: string
  range: any[]
  color?: string
}

const mapToRequestValues = (
  values: CreateRoadmapTimeboxFormValues,
  roadmapId: string,
): CreateRoadmapTimeboxRequest => {
  return {
    $type: 'timebox',
    roadmapId: roadmapId,
    parentId: values.parentId,
    name: values.name,
    description: values.description,
    start: (values.range?.[0] as any)?.format('YYYY-MM-DD'),
    end: (values.range?.[1] as any)?.format('YYYY-MM-DD'),
    color: values.color,
  } as CreateRoadmapTimeboxRequest
}

const CreateRoadmapTimeboxForm = ({
  roadmapId,
  onFormComplete,
  onFormCancel,
}: CreateRoadmapTimeboxFormProps) => {
  const messageApi = useMessage()

  const {
    data: activities,
    error: activitiesError,
  } = useGetRoadmapActivitiesQuery(roadmapId)

  const [createRoadmapTimebox] = useCreateRoadmapItemMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateRoadmapTimeboxFormValues>({
      onSubmit: async (values: CreateRoadmapTimeboxFormValues, form) => {
          try {
            const request = mapToRequestValues(values, roadmapId)
            const response = await createRoadmapTimebox(request)
            if (response.error) throw response.error

            messageApi.success('Roadmap Timebox created successfully.')
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
                  'An error occurred while creating the roadmap timebox. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the roadmap timebox. Please try again.',
      permission: 'Permissions.Roadmaps.Update',
    })

  const selectedParentId = Form.useWatch('parentId', form)
  const selectedRange = Form.useWatch('range', form)
  const parentExpansionHint = getParentExpansionHint(
    findParentActivityRange(activities, selectedParentId),
    selectedRange?.[0],
    selectedRange?.[1],
  )

  // Query error display
  useEffect(() => {
    if (activitiesError) {
      messageApi.error(
        (isApiError(activitiesError) ? activitiesError.supportMessage : undefined) ??
          'An error occurred while loading roadmap activities. Please try again.',
      )
    }
  }, [activitiesError, messageApi])

  return (
    <Modal
      title="Create Roadmap Timebox"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-roadmap-timebox-form"
      >
        <Item
          name="parentId"
          label="Parent Activity"
          hidden={(activities?.length ?? 0) < 1}
        >
          <TreeSelect
            //showSearch // TODO: not working
            treeLine={true}
            placeholder="Please select parent activity"
            allowClear
            treeDefaultExpandAll
            treeData={activities}
            fieldNames={{ label: 'name', value: 'id', children: 'children' }}
          />
        </Item>
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
            onChange={(value) =>
              form.setFieldValue('description', value || '')
            }
            maxLength={2048}
          />
        </Item>
        <Item
          name="range"
          label="Dates"
          rules={[
            { required: true, message: 'Select start and end dates' },
            {
              validator: (_, value) => {
                if (!value || !value[0] || !value[1]) {
                  return Promise.reject(
                    new Error('Start and end dates are required'),
                  )
                }
                const [start, end] = value
                if (!start || !end || end.isBefore(start, 'day')) {
                  return Promise.reject(
                    new Error('End date must be on or after start date'),
                  )
                }
                return Promise.resolve()
              },
            },
          ]}
        >
          <RangePicker />
        </Item>
        {parentExpansionHint && (
          <Alert
            type="info"
            showIcon
            message={parentExpansionHint}
            style={{ marginBottom: 16 }}
          />
        )}
        {/* Hide for now */}
        {/* <Item name="color" label="Color">
          <WaydColorPicker
            color={form.getFieldValue('color')}
            onChange={onColorChange}
          />
        </Item> */}
      </Form>
    </Modal>
  )
}

export default CreateRoadmapTimeboxForm
