'use client'

import { WaydColorPicker } from '@/src/components/common'
import {
  RoadmapActivityDetailsDto,
  RoadmapActivityListDto,
  UpdateRoadmapActivityRequest,
} from '@/src/services/wayd-api'
import {
  useGetRoadmapActivitiesQuery,
  useGetRoadmapItemQuery,
  useGetRoadmapQuery,
  useUpdateRoadmapItemMutation,
} from '@/src/store/features/planning/roadmaps-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Form, Input, Modal, TreeSelect } from 'antd'
import { useEffect, useState } from 'react'
import dayjs from 'dayjs'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import RoadmapColorPicker from './roadmap-color-picker'
import {
  findOwnChildrenSpan,
  findParentActivityRange,
  getChildrenContainmentError,
  getParentExpansionHint,
  isShiftOnlyChange,
} from './roadmap-parent-date-hint'

const { Item } = Form
const { TextArea } = Input
const { RangePicker } = DatePicker

export interface EditRoadmapActivityFormProps {
  activityId: string
  roadmapId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditRoadmapActivityFormValues {
  parentActivityId?: string
  name: string
  description?: string
  range: any[]
  color?: string
}

const mapToRequestValues = (
  values: EditRoadmapActivityFormValues,
  activityId: string,
  roadmapId: string,
): UpdateRoadmapActivityRequest => {
  return {
    $type: 'activity',
    roadmapId: roadmapId,
    itemId: activityId,
    parentId: values.parentActivityId,
    name: values.name,
    description: values.description,
    start: (values.range?.[0] as any)?.format('YYYY-MM-DD'),
    end: (values.range?.[1] as any)?.format('YYYY-MM-DD'),
    color: values.color,
  } satisfies UpdateRoadmapActivityRequest
}

const filterActivities = (activities: RoadmapActivityListDto[], activityId: string): RoadmapActivityListDto[] => {
  return activities
    .filter((a) => a.id !== activityId)
    .map((a) => ({
      ...a,
      children: a.children
        ? filterActivities(
            a.children.filter(
              (c): c is RoadmapActivityListDto => c.$type === 'activity',
            ),
            activityId,
          )
        : [],
    }))
}

const EditRoadmapActivityForm = ({
  activityId,
  roadmapId,
  onFormComplete,
  onFormCancel,
}: EditRoadmapActivityFormProps) => {
  const [activitiesTree, setActivitiesTree] = useState<
    RoadmapActivityListDto[]
  >([])

  const messageApi = useMessage()

  const {
    data: activityData,
    isLoading: activityDataIsLoading,
    error: activityDataError,
  } = useGetRoadmapItemQuery({
    roadmapId: roadmapId,
    itemId: activityId,
  })

  const {
    data: activities,
    isLoading: activitiesIsLoading,
    error: activitiesError,
  } = useGetRoadmapActivitiesQuery(roadmapId)

  const { data: roadmap } = useGetRoadmapQuery(roadmapId)
  const roadmapColors = roadmap?.colors ?? []

  const [updateRoadmapActivity] = useUpdateRoadmapItemMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditRoadmapActivityFormValues>({
      onSubmit: async (values: EditRoadmapActivityFormValues, form) => {
          try {
            const request = mapToRequestValues(values, activityId, roadmapId)
            const response = await updateRoadmapActivity(request)
            if (response.error) throw response.error

            messageApi.success('Roadmap Activity updated successfully.')
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
                  'An error occurred while updating the roadmap activity. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the roadmap activity. Please try again.',
      permission: 'Permissions.Roadmaps.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!activityData || !activities || activitiesIsLoading) return

    const filteredActivities = filterActivities(activities, activityId)
    setActivitiesTree(filteredActivities)

    const activity = activityData as RoadmapActivityDetailsDto
    form.setFieldsValue({
      parentActivityId: activity.parent?.id,
      name: activity.name,
      description: activity.description || '',
      range: [dayjs(activity.start), dayjs(activity.end)],
      color: activity.color,
    })
  }, [activities, activitiesIsLoading, activityData, activityId, form])

  const selectedParentId = Form.useWatch('parentActivityId', form)
  const selectedRange = Form.useWatch('range', form)
  const parentExpansionHint = getParentExpansionHint(
    findParentActivityRange(activities, selectedParentId),
    selectedRange?.[0],
    selectedRange?.[1],
  )
  // This activity's own children constrain its range: it cannot be shrunk behind them.
  const ownChildrenSpan = findOwnChildrenSpan(activities, activityId)

  // Query error display
  useEffect(() => {
    if (activityDataError) {
      messageApi.error(
        (isApiError(activityDataError) ? activityDataError.supportMessage : undefined) ??
          'An error occurred while loading roadmap activity. Please try again.',
      )
    }
    if (activitiesError) {
      messageApi.error(
        (isApiError(activitiesError) ? activitiesError.supportMessage : undefined) ??
          'An error occurred while loading roadmap activities. Please try again.',
      )
    }
  }, [activitiesError, activityDataError, messageApi])

  return (
    <Modal
      title="Edit Roadmap Activity"
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
        name="update-roadmap-activity-form"
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
                const originalStart = activityData?.start ? dayjs(activityData.start) : null
                const originalEnd = activityData?.end ? dayjs(activityData.end) : null
                const isShift = isShiftOnlyChange(originalStart, originalEnd, start, end)

                if (!isShift) {
                  const containmentError = getChildrenContainmentError(
                    ownChildrenSpan,
                    start,
                    end,
                  )
                  if (containmentError) {
                    return Promise.reject(new Error(containmentError))
                  }
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
        <Item name="color" label="Color">
          {roadmapColors.length > 0 ? (
            <RoadmapColorPicker entries={roadmapColors} />
          ) : (
            <WaydColorPicker />
          )}
        </Item>
      </Form>
    </Modal>
  )
}

export default EditRoadmapActivityForm
