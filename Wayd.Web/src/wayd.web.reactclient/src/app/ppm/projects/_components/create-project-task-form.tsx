'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CreateProjectTaskRequest } from '@/src/services/wayd-api'
import { type Dayjs } from 'dayjs'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import {
  useCreateProjectTaskMutation,
  useGetTaskPriorityOptionsQuery,
  useGetTaskTypeOptionsQuery,
  useGetParentTaskOptionsQuery,
  useGetTaskStatusOptionsQuery,
} from '@/src/store/features/ppm/project-tasks-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import {
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Radio,
  TreeSelect,
} from 'antd'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input
const { Group: RadioGroup } = Radio
const { RangePicker } = DatePicker

export interface CreateProjectTaskFormProps {
  projectIdOrKey: string
  parentTaskId?: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateProjectTaskFormValues {
  name: string
  description?: string
  type: number
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
  values: CreateProjectTaskFormValues,
): CreateProjectTaskRequest => {
  return {
    name: values.name,
    description: values.description,
    typeId: values.type,
    statusId: values.status,
    priorityId: values.priority,
    assigneeIds: values.assigneeIds,
    progress: values.progress,
    parentId: values.parentId,
    plannedStart: (values.plannedRange?.[0] as any)?.format('YYYY-MM-DD'),
    plannedEnd: (values.plannedRange?.[1] as any)?.format('YYYY-MM-DD'),
    plannedDate: values.plannedDate?.format('YYYY-MM-DD'),
    estimatedEffortHours: values.estimatedEffortHours,
  } as unknown as CreateProjectTaskRequest
}

const CreateProjectTaskForm = ({
  projectIdOrKey,
  parentTaskId,
  onFormComplete,
  onFormCancel,
}: CreateProjectTaskFormProps) => {
  const messageApi = useMessage()

  const [createProjectTask] = useCreateProjectTaskMutation()

  const { data: typeOptions = [] } = useGetTaskTypeOptionsQuery()

  const {
    data: employeeData,
  } = useGetEmployeeOptionsQuery(false)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateProjectTaskFormValues>({
      onSubmit: async (values: CreateProjectTaskFormValues, form) => {
          try {
            const request = mapToRequestValues(values)
            const response = await createProjectTask({
              projectIdOrKey,
              request,
            })
            if (response.error) throw response.error

            messageApi.success(
              'Task created successfully. Task key: ' + response.data!.key,
            )
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
                  'An error occurred while creating the project task. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the task. Please try again.',
      permission: 'Permissions.Projects.Create',
    })

  const selectedType = Form.useWatch('type', form)
  // Find the Milestone value dynamically from options
  const milestoneValue = typeOptions.find(
    (opt) => opt.label === 'Milestone',
  )?.value
  const isMilestone = selectedType === milestoneValue

  const { data: statusOptions = [] } = useGetTaskStatusOptionsQuery({
    forMilestone: isMilestone,
  })

  const { data: priorityOptions = [] } = useGetTaskPriorityOptionsQuery()

  const { data: parentTaskOptions = [] } = useGetParentTaskOptionsQuery({
    projectIdOrKey,
  })

  // Set parentTaskId if provided
  useEffect(() => {
    if (parentTaskId) {
      form.setFieldValue('parentId', parentTaskId)
    }
  }, [parentTaskId, form])

  // Reset status to "Not Started" (1) if user changes type to Milestone while status is "In Progress" (2)
  useEffect(() => {
    const currentStatus = form.getFieldValue('status')
    if (isMilestone && currentStatus === 2) {
      form.setFieldValue('status', 1)
    }
  }, [isMilestone, form])

  return (
    <Modal
      title="Create Task"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || isSaving }}
      okText="Create"
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
        name="create-project-task-form"
        initialValues={{
          type: 1, // Default to 'Task' type
          status: 1, // Default to 'Not Started' status
          priority: 2, // Default to 'Medium' priority
          parentId: parentTaskId,
          progress: 0,
        }}
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
            { max: 128 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 2048 }]}>
          <MarkdownEditor maxLength={2048} />
        </Item>

        <Item
          name="type"
          label="Type"
          rules={[{ required: true, message: 'Please select a type' }]}
        >
          <RadioGroup
            options={typeOptions}
            optionType="button"
            buttonStyle="solid"
          />
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
            <Item name="plannedRange" label="Planned Date Range">
              <RangePicker style={{ width: '60%' }} format="MMM D, YYYY" />
            </Item>

            <Item name="progress" label="Progress %">
              <InputNumber min={0} max={100} style={{ width: '33%' }} />
            </Item>

            <Item name="estimatedEffortHours" label="Estimated Effort (hours)">
              <InputNumber min={0} step={0.25} style={{ width: '33%' }} />
            </Item>
          </>
        ) : (
          <Item
            name="plannedDate"
            label="Planned Date"
            rules={[{ required: true }]}
          >
            <DatePicker style={{ width: '60%' }} format="MMM D, YYYY" />
          </Item>
        )}
      </Form>
    </Modal>
  )
}

export default CreateProjectTaskForm
