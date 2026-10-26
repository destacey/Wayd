'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeDeskDto, IntakeDeskRole } from '@/src/services/wayd-api'
import {
  useGetIntakeEmployeeOptionsQuery,
  useUpdateIntakeDeskMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Modal } from 'antd'
import { FC, useState } from 'react'
import { memberIds, toUpdateRequest } from './desk-update'

const { Item } = Form

export interface EditIntakeDeskMembersFormProps {
  desk: IntakeDeskDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditIntakeDeskMembersFormValues {
  adminIds: string[]
  agentIds: string[]
}

/**
 * Names the desk's Desk Admins and Agents. Offered where the desk's `canManage` says so; Desk Admins only
 * the Demand Management Admin changes, which the desk's `canAdminister` answers, so a Desk Admin can name
 * agents but never raise anyone's access to running the desk.
 */
const EditIntakeDeskMembersForm: FC<EditIntakeDeskMembersFormProps> = ({
  desk: openedDesk,
  onFormComplete,
  onFormCancel,
}) => {
  // The desk as it was when the form opened. The page refetches the desk while the form is open — a failed
  // save does too — and sending its newer revision with values edited from the older one would let a retry
  // overwrite whatever changed in between. Holding this snapshot keeps every retry refused until a reload.
  const [desk] = useState(openedDesk)
  const messageApi = useMessage()
  const [updateDesk] = useUpdateIntakeDeskMutation()
  const { data: employees } = useGetIntakeEmployeeOptionsQuery()

  // Members who have since become inactive are not offered for selection, but stay on the desk until removed.
  const employeeOptions = [
    ...(employees ?? []).map((e) => ({ label: e.name, value: e.id })),
    ...desk.members
      .filter((m) => !(employees ?? []).some((e) => e.id === m.employee.id))
      .map((m) => ({ label: m.employee.name, value: m.employee.id })),
  ]

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditIntakeDeskMembersFormValues>({
      onSubmit: async (values, form) => {
        try {
          await updateDesk(
            toUpdateRequest(desk, {
              members: [
                ...(values.adminIds ?? []).map((employeeId) => ({
                  employeeId,
                  role: IntakeDeskRole.Admin,
                })),
                ...(values.agentIds ?? []).map((employeeId) => ({
                  employeeId,
                  role: IntakeDeskRole.Agent,
                })),
              ],
            }),
          ).unwrap()
          messageApi.success('Members updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to update the members.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to update the members.',
    })

  return (
    <Modal
      title="Edit Members"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={640}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-intake-desk-members-form"
        initialValues={{
          adminIds: memberIds(desk, IntakeDeskRole.Admin),
          agentIds: memberIds(desk, IntakeDeskRole.Agent),
        }}
      >
        <Item
          name="adminIds"
          label="Desk Admins"
          extra={
            desk.canAdminister
              ? 'Run the desk: change its details and settings, name its agents, and delete its requests.'
              : 'Only the Demand Management Admin can name or remove Desk Admins.'
          }
        >
          <EmployeeSelect
            allowMultiple
            disabled={!desk.canAdminister}
            employees={employeeOptions}
          />
        </Item>
        <Item
          name="agentIds"
          label="Agents"
          extra="Act on the desk's requests. Removing someone unassigns them from the requests they own here."
          dependencies={['adminIds']}
          rules={[
            ({ getFieldValue }) => ({
              validator: (_, agentIds: string[] | undefined) => {
                const adminIds: string[] = getFieldValue('adminIds') ?? []
                return (agentIds ?? []).some((id) => adminIds.includes(id))
                  ? Promise.reject(
                      new Error(
                        'Someone holds one role on a desk: a Desk Admin already does everything an agent does.',
                      ),
                    )
                  : Promise.resolve()
              },
            }),
          ]}
        >
          <EmployeeSelect allowMultiple employees={employeeOptions} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditIntakeDeskMembersForm
