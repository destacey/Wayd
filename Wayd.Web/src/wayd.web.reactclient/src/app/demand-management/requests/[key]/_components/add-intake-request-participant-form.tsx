'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import {
  useAddIntakeRequestParticipantMutation,
  useGetIntakeEmployeeOptionsQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Modal, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Paragraph } = Typography

export interface AddIntakeRequestParticipantFormProps {
  request: IntakeRequestDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddParticipantFormValues {
  employeeId: string
}

/** Gives someone access to the request. Offered only where the server's `canAddParticipants` says so. */
const AddIntakeRequestParticipantForm: FC<
  AddIntakeRequestParticipantFormProps
> = ({ request, onFormComplete, onFormCancel }) => {
  const messageApi = useMessage()
  const [addParticipant] = useAddIntakeRequestParticipantMutation()
  const { data: employees } = useGetIntakeEmployeeOptionsQuery()

  const alreadyIn = new Set([
    request.summary.requester.id,
    ...(request.substance
      ? [
          request.substance.submittedBy.id,
          ...request.substance.participants.map((p) => p.employee.id),
        ]
      : []),
  ])
  const options = (employees ?? [])
    .filter((e) => !alreadyIn.has(e.id))
    .map((e) => ({ label: e.name, value: e.id }))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddParticipantFormValues>({
      onSubmit: async (values, form) => {
        try {
          await addParticipant({
            requestId: request.summary.id,
            employeeId: values.employeeId,
          }).unwrap()
          messageApi.success('Participant added.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to add the participant.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to add the participant.',
    })

  return (
    <Modal
      title="Add Participant"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        A participant can read this request and reply to it. They cannot change
        it.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="add-intake-request-participant-form"
      >
        <Item
          name="employeeId"
          label="Person"
          rules={[{ required: true, message: 'Choose someone to add.' }]}
        >
          <EmployeeSelect employees={options} />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddIntakeRequestParticipantForm
