'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ProjectDeliverableDto,
  ProjectPlanNodeDto,
} from '@/src/services/wayd-api'
import { useSetProjectDeliverableTasksMutation } from '@/src/store/features/ppm/project-deliverables-api'
import { toApiError } from '@/src/utils'
import { Alert, Form, Modal, TreeSelect, Typography } from 'antd'
import { useEffect } from 'react'

const { Item } = Form
const { Text } = Typography

export interface SetDeliverableTasksFormProps {
  projectId: string
  deliverable: ProjectDeliverableDto
  /** The project's plan, which the tasks are picked from and the current links are read off. */
  plan: ProjectPlanNodeDto[]
  onFormComplete: () => void
  onFormCancel: () => void
}

interface SetDeliverableTasksFormValues {
  taskIds: string[]
}

interface TaskTreeOption {
  value: string
  title: string
  selectable?: boolean
  children: TaskTreeOption[]
}

/** The plan as picker options: stages head the tree but cannot be picked, since only tasks serve a deliverable. */
export const toTreeOptions = (plan: ProjectPlanNodeDto[]): TaskTreeOption[] => {
  const toOption = (node: ProjectPlanNodeDto): TaskTreeOption => ({
    value: node.id,
    title: node.nodeType === 'Stage' ? node.name : `${node.wbs} ${node.name}`,
    selectable: node.nodeType !== 'Stage',
    children: (node.children ?? []).map(toOption),
  })
  return plan.map(toOption)
}

/** Every task of the plan linked to the deliverable, in plan order. */
export const linkedTasks = (
  plan: ProjectPlanNodeDto[],
  deliverableId: string,
): ProjectPlanNodeDto[] => {
  const tasks: ProjectPlanNodeDto[] = []
  const visit = (node: ProjectPlanNodeDto) => {
    if (node.deliverableIds?.includes(deliverableId)) tasks.push(node)
    ;(node.children ?? []).forEach(visit)
  }
  plan.forEach(visit)
  return tasks
}

/**
 * Sets which plan tasks serve a deliverable, replacing its links. Picking a
 * summary task counts its subtasks toward the deliverable, so they need no
 * link of their own. Linking is plan work: it needs only the permission to
 * update projects, not a role on the project.
 */
const SetDeliverableTasksForm = ({
  projectId,
  deliverable,
  plan,
  onFormComplete,
  onFormCancel,
}: SetDeliverableTasksFormProps) => {
  const messageApi = useMessage()
  const [setDeliverableTasks] = useSetProjectDeliverableTasksMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<SetDeliverableTasksFormValues>({
      onSubmit: async (values) => {
        try {
          await setDeliverableTasks({
            projectId,
            deliverableId: deliverable.id,
            taskIds: values.taskIds ?? [],
          }).unwrap()
          messageApi.success('Linked tasks saved.')
          return true
        } catch (error) {
          messageApi.error(
            toApiError(error).detail ?? 'Failed to save the linked tasks.',
          )
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to save the linked tasks.',
      permission: 'Permissions.Projects.Update',
    })

  useEffect(() => {
    form.setFieldsValue({
      taskIds: linkedTasks(plan, deliverable.id).map((t) => t.id),
    })
  }, [form, plan, deliverable.id])

  const hasTasks = plan.some((stage) => (stage.children ?? []).length > 0)

  return (
    <Modal
      title="Link Tasks"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || isSaving || !hasTasks }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={560}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="set-deliverable-tasks-form"
      >
        <Text>
          Choose the plan tasks that deliver{' '}
          <Text strong>{deliverable.name}</Text>. Its progress is worked out
          from them, weighted by planned duration.
        </Text>
        {hasTasks ? (
          <Item name="taskIds" label="Tasks" style={{ marginTop: 12 }}>
            <TreeSelect
              multiple
              allowClear
              treeCheckable={false}
              treeData={toTreeOptions(plan)}
              treeDefaultExpandAll
              placeholder="Select tasks"
              showSearch={{
                filterTreeNode: (input, node) =>
                  node.title
                    ?.toString()
                    .toLowerCase()
                    .includes(input.toLowerCase()) ?? false,
              }}
            />
          </Item>
        ) : (
          <Alert
            type="info"
            showIcon
            title="This project's plan has no tasks yet."
            style={{ marginTop: 12 }}
          />
        )}
      </Form>
    </Modal>
  )
}

export default SetDeliverableTasksForm
