'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useLinkedEmployee, useModalForm } from '@/src/hooks'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import {
  useChangeIntakeRequestAssigneeMutation,
  useGetIntakeDeskQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Button, Form, Modal, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Paragraph } = Typography

export interface ChangeIntakeRequestAssigneeFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AssigneeFormValues {
  assigneeId?: string
}

/**
 * Gives the request an owner from among its desk's members, or clears it. Offered only where the server's
 * `canChangeAssignee` says so. The assignee owns the next move; they are not an approver, and the copy says
 * so because the two are easily confused.
 */
const ChangeIntakeRequestAssigneeForm: FC<
  ChangeIntakeRequestAssigneeFormProps
> = ({ request, onFormComplete, onFormCancel }) => {
  const messageApi = useMessage()
  const { employeeId } = useLinkedEmployee()
  const [changeAssignee] = useChangeIntakeRequestAssigneeMutation()
  const { summary } = request
  const { data: desk, isLoading: deskLoading } = useGetIntakeDeskQuery(
    String(summary.desk.key),
  )

  const members = (desk?.members ?? []).map((m) => ({
    label: m.employee.name,
    value: m.employee.id,
  }))
  const readerIsMember = members.some((m) => m.value === employeeId)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AssigneeFormValues>({
      onSubmit: async (values) => {
        try {
          await changeAssignee({
            requestId: summary.id,
            assigneeId: values.assigneeId || undefined,
          }).unwrap()
          messageApi.success(
            values.assigneeId ? 'Request assigned.' : 'Request unassigned.',
          )
          return true
        } catch (error) {
          messageApi.error(
            toApiError(error).detail ?? 'Failed to change the assignee.',
          )
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to change the assignee.',
    })

  return (
    <Modal
      title="Change Assignee"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        The assignee owns the next move on this request. Anyone who works the{' '}
        {summary.desk.name} desk can be assigned. Assigning someone does not
        make them its approver.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-intake-request-assignee-form"
        initialValues={{ assigneeId: summary.assignee?.id }}
      >
        <Item
          name="assigneeId"
          label="Assignee"
          extra="Leave it empty to unassign the request."
        >
          <EmployeeSelect
            employees={members}
            placeholder={deskLoading ? 'Loading…' : 'Not assigned'}
          />
        </Item>
        {readerIsMember && (
          <Button
            type="link"
            size="small"
            style={{ paddingInline: 0 }}
            onClick={() => form.setFieldValue('assigneeId', employeeId)}
          >
            Assign to me
          </Button>
        )}
      </Form>
    </Modal>
  )
}

export default ChangeIntakeRequestAssigneeForm
