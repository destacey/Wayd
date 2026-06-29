'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { UpdateProjectTaskRequest } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import {
  useGetProjectTaskQuery,
  useGetTaskStatusOptionsQuery,
  useGetTaskPriorityOptionsQuery,
  useGetParentTaskOptionsQuery,
  useUpdateProjectTaskMutation,
} from '@/src/store/features/ppm/project-tasks-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useGetProjectPlanTreeQuery } from '@/src/store/features/ppm/projects-api'
import {
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Radio,
  TreeSelect,
  Alert,
} from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect } from 'react'
import {
  findParentPlanNodeRange,
  getParentExpansionHint,
  getMilestoneParentExpansionHint,
  findOwnChildrenSpan,
  getChildrenContainmentError,
  isShiftOnlyChange,
} from './project-parent-date-hint'

const { Item } = Form
const { TextArea } = Input
const { Group: RadioGroup } = Radio
const { RangePicker } = DatePicker

export interface EditProjectTaskFormProps {
  projectIdOrKey: string
  taskIdOrKey: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditProjectTaskFormValues {
  name: string
  description?: string
  status: number
  priority?: number
  assigneeIds: string[]
  progress?: number
  parentId?: string
  plannedRange?: any[]
  plannedDate?: Dayjs
  estimatedEffortHours?: number
}

const mapToRequestValues = (
  values: EditProjectTaskFormValues,
  taskId: string,
): UpdateProjectTaskRequest => {
  return {
    id: taskId,
    name: values.name,
    description: values.description,
    statusId: values.status,
    priorityId: values.priority,
    assigneeIds: values.assigneeIds,
    progress: values.progress,
    parentId: values.parentId,
    plannedStart: (values.plannedRange?.[0] as any)?.format('YYYY-MM-DD'),
    plannedEnd: (values.plannedRange?.[1] as any)?.format('YYYY-MM-DD'),
    plannedDate: values.plannedDate?.format('YYYY-MM-DD'),
    estimatedEffortHours: values.estimatedEffortHours,
  } as unknown as UpdateProjectTaskRequest
}

const EditProjectTaskForm = ({
  projectIdOrKey,
  taskIdOrKey,
  onFormComplete,
  onFormCancel,
}: EditProjectTaskFormProps) => {
  const messageApi = useMessage()

  const [updateProjectTask] = useUpdateProjectTaskMutation()

  const {
    data: taskData,
    isLoading,
  } = useGetProjectTaskQuery({
    projectIdOrKey,
    taskIdOrKey,
  })

  const taskType = taskData?.type?.name
  const isMilestone = taskType === 'Milestone'



  const {
    data: employeeData,
  } = useGetEmployeeOptionsQuery(true)

  const { data: statusOptions = [] } = useGetTaskStatusOptionsQuery({
    forMilestone: isMilestone,
  })

  const { data: priorityOptions = [] } = useGetTaskPriorityOptionsQuery()

  const { data: parentTaskOptions = [] } = useGetParentTaskOptionsQuery({
    projectIdOrKey,
    excludeTaskId: taskData?.id,
  })

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditProjectTaskFormValues>({
      onSubmit: async (values: EditProjectTaskFormValues, form) => {
          if (!taskData) {
            messageApi.error('Task data is not loaded.')
            return false
          }

          try {
            const request = mapToRequestValues(values, taskData.id)
            const response = await updateProjectTask({
              projectIdOrKey,
              request,
              cacheKey: taskIdOrKey,
            })
            if (response.error) throw response.error

            messageApi.success('Task updated successfully.')
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
                  'An error occurred while update the project task. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the task. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  const { data: planTree } = useGetProjectPlanTreeQuery(projectIdOrKey)
  const selectedParentId = Form.useWatch('parentId', form)
  const selectedRange = Form.useWatch('plannedRange', form)
  const selectedDate = Form.useWatch('plannedDate', form)

  const parentRange = findParentPlanNodeRange(planTree, selectedParentId)
  const parentExpansionHint = isMilestone
    ? getMilestoneParentExpansionHint(parentRange, selectedDate)
    : getParentExpansionHint(parentRange, selectedRange?.[0], selectedRange?.[1])

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!taskData) return

    const plannedRange =
      taskData.plannedStart && taskData.plannedEnd
        ? [dayjs(taskData.plannedStart), dayjs(taskData.plannedEnd)]
        : undefined

    form.setFieldsValue({
      name: taskData.name,
      description: taskData.description,
      status: taskData.status.id,
      priority: taskData.priority?.id,
      assigneeIds: taskData.assignees.map((a: any) => a.id),
      progress: taskData.progress,
      parentId: taskData.parent?.id ?? taskData.projectPhaseId,
      plannedRange,
      plannedDate: taskData.plannedDate
        ? dayjs(taskData.plannedDate)
        : undefined,
      estimatedEffortHours: taskData.estimatedEffortHours,
    })
  }, [taskData, form])

  if (isLoading) {
    return null
  }

  return (
    <Modal
      title="Edit Task"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || isSaving }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={500}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-project-task-form"
      >
        <Item name="parentId" label="Parent Task">
          <TreeSelect
            allowClear
            placeholder="Select parent task (optional)"
            notFoundContent="No tasks found"
            treeData={parentTaskOptions}
            treeDefaultExpandAll
            showSearch={{
              filterTreeNode: (input, node) =>
                node.title
                  ?.toString()
                  .toLowerCase()
                  .includes(input.toLowerCase()) ?? false,
            }}
          />
        </Item>
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128, message: 'Name cannot exceed 256 characters' },
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
            { max: 2048, message: 'Description cannot exceed 2048 characters' },
          ]}
        >
          <MarkdownEditor maxLength={2048} />
        </Item>

        <Item
          name="status"
          label="Status"
          rules={[{ required: true, message: 'Please select a status' }]}
        >
          <RadioGroup
            options={statusOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>

        <Item
          name="priority"
          label="Priority"
          rules={[{ required: true, message: 'Please select a priority' }]}
        >
          <RadioGroup
            options={priorityOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>

        <Item name="assigneeIds" label="Assignees">
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select Assignees"
          />
        </Item>

        {!isMilestone ? (
          <>
            <Item
              name="plannedRange"
              label="Planned Date Range"
              rules={[
                {
                  validator: (_, value) => {
                    const childrenSpan = findOwnChildrenSpan(planTree, taskData?.id)
                    if (childrenSpan) {
                      if (!value || !value[0] || !value[1]) {
                        return Promise.reject(
                          new Error('Planned dates cannot be cleared when child items have dates.')
                        )
                      }
                      const start = value[0]
                      const end = value[1]
                      const originalStart = taskData?.plannedStart ? dayjs(taskData.plannedStart) : null
                      const originalEnd = taskData?.plannedEnd ? dayjs(taskData.plannedEnd) : null
                      const isShift = isShiftOnlyChange(originalStart, originalEnd, start, end)

                      if (!isShift) {
                        const containmentError = getChildrenContainmentError(childrenSpan, start, end)
                        if (containmentError) {
                          return Promise.reject(new Error(containmentError))
                        }
                      }
                    }
                    return Promise.resolve()
                  }
                }
              ]}
            >
              <RangePicker style={{ width: '60%' }} format="MMM D, YYYY" />
            </Item>

            <Item name="progress" label="Progress %">
              <InputNumber min={0} max={100} style={{ width: '33%' }} />
            </Item>

            {/* TODO: the validation error is not displaying for this field
              even though it's being set in the form correctly (same as create form) */}
            <Item name="estimatedEffortHours" label="Estimated Effort (hours)">
              <InputNumber min={0} step={0.25} style={{ width: '33%' }} />
            </Item>
          </>
        ) : (
          <Item name="plannedDate" label="Planned Date">
            <DatePicker style={{ width: '60%' }} format="MMM D, YYYY" />
          </Item>
        )}
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

export default EditProjectTaskForm
