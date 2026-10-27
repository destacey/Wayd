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
import { Button, ConfigProvider, Form, Grid, Modal, Typography } from 'antd'
import { FC } from 'react'
import { deskPhrase } from '../../../_components/desk-status'
import { TOUCH_THEME } from '../../../_components/touch-theme'

const { Item } = Form
const { Paragraph } = Typography
const { useBreakpoint } = Grid

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
  const phone = useBreakpoint().md === false
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

  const dialog = (
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
        The assignee owns the next move on this request. Anyone who works{' '}
        {deskPhrase(summary.desk.name)} can be assigned. Assigning someone does
        not make them its approver.
      </Paragraph>
      <Form
        form={form}
        size={phone ? undefined : 'small'}
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
            size={phone ? undefined : 'small'}
            style={{ paddingInline: 0 }}
            onClick={() => form.setFieldValue('assigneeId', employeeId)}
          >
            Assign to me
          </Button>
        )}
      </Form>
    </Modal>
  )

  // On a phone the dialog is tapped, so its controls are touch-sized. Always rendered, with the theme
  // switched, so crossing the breakpoint never remounts the dialog.
  return (
    <ConfigProvider theme={phone ? TOUCH_THEME : undefined}>
      {dialog}
    </ConfigProvider>
  )
}

export default ChangeIntakeRequestAssigneeForm
