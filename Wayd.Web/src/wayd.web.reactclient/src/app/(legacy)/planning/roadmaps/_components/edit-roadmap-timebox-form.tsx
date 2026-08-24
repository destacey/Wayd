import {
  RoadmapActivityListDto,
  RoadmapTimeboxDetailsDto,
  RoadmapTimeboxListDto,
  UpdateRoadmapTimeboxRequest,
} from '@/src/services/wayd-api'
import {
  useGetRoadmapActivitiesQuery,
  useGetRoadmapItemQuery,
  useUpdateRoadmapItemMutation,
} from '@/src/store/features/planning/roadmaps-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Form, Input, Modal, TreeSelect } from 'antd'
import { useEffect, useState } from 'react'
import dayjs from 'dayjs'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  findParentActivityRange,
  getParentExpansionHint,
} from './roadmap-parent-date-hint'

const { Item } = Form
const { TextArea } = Input
const { RangePicker } = DatePicker

export interface EditRoadmapTimeboxFormProps {
  timeboxId: string
  roadmapId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditRoadmapTimeboxFormValues {
  parentActivityId?: string
  name: string
  description?: string
  range: any[]
  color?: string
}

const mapToRequestValues = (
  values: EditRoadmapTimeboxFormValues,
  timeboxId: string,
  roadmapId: string,
): UpdateRoadmapTimeboxRequest => {
  return {
    $type: 'timebox',
    roadmapId: roadmapId,
    itemId: timeboxId,
    parentId: values.parentActivityId,
    name: values.name,
    description: values.description,
    start: (values.range?.[0] as any)?.format('YYYY-MM-DD'),
    end: (values.range?.[1] as any)?.format('YYYY-MM-DD'),
    color: values.color,
  } as UpdateRoadmapTimeboxRequest
}

const EditRoadmapTimeboxForm = ({
  timeboxId,
  roadmapId,
  onFormComplete,
  onFormCancel,
}: EditRoadmapTimeboxFormProps) => {
  const [activitiesTree, setActivitiesTree] = useState<RoadmapTimeboxListDto[]>(
    [],
  )

  const messageApi = useMessage()

  const {
    data: timeboxData,
    isLoading: timeboxDataIsLoading,
    error: timeboxDataError,
  } = useGetRoadmapItemQuery({
    roadmapId: roadmapId,
    itemId: timeboxId,
  })

  const {
    data: activities,
    isLoading: activitiesIsLoading,
    error: activitiesError,
  } = useGetRoadmapActivitiesQuery(roadmapId)

  const [updateRoadmapTimebox] = useUpdateRoadmapItemMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditRoadmapTimeboxFormValues>({
      onSubmit: async (values: EditRoadmapTimeboxFormValues, form) => {
          try {
            const request = mapToRequestValues(values, timeboxId, roadmapId)
            const response = await updateRoadmapTimebox(request)
            if (response.error) throw response.error

            messageApi.success('Roadmap Timebox updated successfully.')
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
                  'An error occurred while updating the roadmap timebox. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the roadmap timebox. Please try again.',
      permission: 'Permissions.Roadmaps.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!timeboxData || !activities || activitiesIsLoading) return

    setActivitiesTree(activities)

    const timebox = timeboxData as RoadmapTimeboxDetailsDto
    form.setFieldsValue({
      parentActivityId: timebox.parent?.id,
      name: timebox.name,
      description: timebox.description || '',
      range: [dayjs(timebox.start), dayjs(timebox.end)],
      color: timebox.color,
    })
  }, [activities, activitiesIsLoading, timeboxData, form])

  const selectedParentId = Form.useWatch('parentActivityId', form)
  const selectedRange = Form.useWatch('range', form)
  const parentExpansionHint = getParentExpansionHint(
    findParentActivityRange(
      activities as RoadmapActivityListDto[] | undefined,
      selectedParentId,
    ),
    selectedRange?.[0],
    selectedRange?.[1],
  )

  // Query error display
  useEffect(() => {
    if (timeboxDataError) {
      messageApi.error(
        (isApiError(timeboxDataError) ? timeboxDataError.supportMessage : undefined) ??
          'An error occurred while loading roadmap timebox. Please try again.',
      )
    }
    if (activitiesError) {
      messageApi.error(
        (isApiError(activitiesError) ? activitiesError.supportMessage : undefined) ??
          'An error occurred while loading roadmap activities. Please try again.',
      )
    }
  }, [activitiesError, timeboxDataError, messageApi])

  return (
    <Modal
      title="Edit Roadmap Timebox"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="update-roadmap-timebox-form"
      >
        <Item name="parentActivityId" label="Parent Activity">
          <TreeSelect
            //showSearch // TODO: not working
            loading={activitiesIsLoading}
            treeLine={true}
            placeholder="Select parent activity"
            allowClear
            treeDefaultExpandAll
            treeData={activitiesTree}
            fieldNames={{ label: 'name', value: 'id', children: 'children' }}
            value={form.getFieldValue('parentActivityId')}
          />
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
      </Form>
    </Modal>
  )
}

export default EditRoadmapTimeboxForm
