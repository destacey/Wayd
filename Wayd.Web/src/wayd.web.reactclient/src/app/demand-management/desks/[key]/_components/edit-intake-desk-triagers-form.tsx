'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeDeskDto } from '@/src/services/wayd-api'
import { useUpdateIntakeDeskMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Alert, Form, Modal, Transfer } from 'antd'
import { FC, useState } from 'react'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { toUpdateRequest, triagerIds } from './desk-update'

const { Item } = Form

export interface EditIntakeDeskTriagersFormProps {
  desk: IntakeDeskDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditIntakeDeskTriagersFormValues {
  triagerIds: string[]
}

/**
 * Names the members who triage the desk's new requests — told when one arrives unassigned, when its
 * requester adds to it, and when it leaves the queue. Chosen person by person, whatever their role, because
 * who dispatches a desk's work is rarely everyone holding one role.
 */
const EditIntakeDeskTriagersForm: FC<EditIntakeDeskTriagersFormProps> = ({
  desk: openedDesk,
  onFormComplete,
  onFormCancel,
}) => {
  // The desk as it was when the form opened, so a retry after a refetch is still refused as stale.
  const [desk] = useState(openedDesk)
  const messageApi = useMessage()
  const [updateDesk] = useUpdateIntakeDeskMutation()

  const members = desk.members
    .map((m) => ({ key: m.employee.id, title: m.employee.name }))
    .sort((a, b) => caseInsensitiveCompare(a.title, b.title))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditIntakeDeskTriagersFormValues>({
      onSubmit: async (values, form) => {
        try {
          await updateDesk(
            toUpdateRequest(desk, { triagerIds: values.triagerIds ?? [] }),
          ).unwrap()
          messageApi.success('Triagers updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to update the triagers.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to update the triagers.',
    })

  // Undefined until the field registers, so the desk's own triagers stand in rather than flashing the warning.
  const chosen: string[] = Form.useWatch('triagerIds', form) ?? triagerIds(desk)

  return (
    <Modal
      title="Edit Triagers"
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
        name="edit-intake-desk-triagers-form"
        initialValues={{ triagerIds: triagerIds(desk) }}
      >
        <Item
          name="triagerIds"
          valuePropName="targetKeys"
          extra="Triagers are told when a request arrives on this desk unassigned, when its requester renames it, changes its details or need-by date, or replies, and when it is taken, withdrawn or closed. Anyone on the desk can triage, whatever their role."
        >
          <Transfer
            dataSource={members}
            titles={['Members', 'Triage']}
            render={(item) => item.title}
            showSearch
            filterOption={(input, item) =>
              item.title.toLowerCase().includes(input.toLowerCase())
            }
            locale={{ notFoundContent: 'Nobody' }}
          />
        </Item>
        {chosen.length === 0 && (
          <Alert
            type="warning"
            showIcon
            title="Nobody is told about new requests; they are triaged from the board."
          />
        )}
      </Form>
    </Modal>
  )
}

export default EditIntakeDeskTriagersForm
