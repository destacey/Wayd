'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ApplyIncidentWorkflowActionRequest,
  IncidentDto,
  IncidentWorkflowAction,
} from '@/src/services/wayd-api'
import { useApplyIncidentWorkflowActionMutation } from '@/src/store/features/product-management/incidents-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal } from 'antd'

const { Item } = Form
const { TextArea } = Input

export interface CancelIncidentFormProps {
  incident: IncidentDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CancelIncidentFormValues {
  reason?: string
}

const CancelIncidentForm = ({
  incident,
  onFormComplete,
  onFormCancel,
}: CancelIncidentFormProps) => {
  const messageApi = useMessage()
  const [applyAction] = useApplyIncidentWorkflowActionMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CancelIncidentFormValues>({
      onSubmit: async (values: CancelIncidentFormValues, form) => {
        try {
          const request: ApplyIncidentWorkflowActionRequest = {
            action: IncidentWorkflowAction.Cancel,
            reason: values.reason,
          }

          const response = await applyAction({
            id: incident.id,
            idOrKey: String(incident.key),
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Incident canceled.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Could not cancel this incident.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Could not cancel this incident.',
      permission: 'Permissions.Incidents.Update',
    })

  return (
    <Modal
      title="Cancel incident"
      open={isOpen}
      onOk={handleOk}
      onCancel={handleCancel}
      okText="Cancel incident"
      okButtonProps={{ disabled: !isValid, danger: true }}
      confirmLoading={isSaving}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} layout="vertical">
        <Item name="reason" label="Reason (optional)">
          <TextArea rows={3} maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default CancelIncidentForm
