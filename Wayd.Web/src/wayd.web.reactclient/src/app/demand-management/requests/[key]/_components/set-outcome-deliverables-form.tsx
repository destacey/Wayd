'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeRequestOutcomeDto } from '@/src/services/wayd-api'
import { useSetIntakeRequestOutcomeDeliverablesMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Form, Modal, Typography } from 'antd'
import { FC } from 'react'
import OutcomeDeliverablesSelect from './outcome-deliverables-select'

const { Item } = Form
const { Paragraph } = Typography

export interface SetOutcomeDeliverablesFormProps {
  requestId: string
  /** An existing project scope outcome whose project still has a copy. */
  outcome: IntakeRequestOutcomeDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface SetOutcomeDeliverablesFormValues {
  deliverableIds: string[]
}

/**
 * Changes which of the project's deliverables an existing project scope outcome is mapped to. Sends this one
 * outcome's whole mapping, which is all the endpoint replaces, so another outcome changed meanwhile is untouched.
 */
const SetOutcomeDeliverablesForm: FC<SetOutcomeDeliverablesFormProps> = ({
  requestId,
  outcome,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [setDeliverables] = useSetIntakeRequestOutcomeDeliverablesMutation()

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<SetOutcomeDeliverablesFormValues>({
      onSubmit: async (values) => {
        try {
          await setDeliverables({
            requestId,
            targetId: outcome.targetId,
            deliverableIds: values.deliverableIds ?? [],
          }).unwrap()
          messageApi.success('Deliverables updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          messageApi.error(
            apiError.detail ?? 'Failed to update the deliverables.',
          )
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to update the deliverables.',
    })

  return (
    <Modal
      title="Edit Deliverables"
      open={isOpen}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        The deliverables of {outcome.project?.name ?? 'the project'} that meet
        this request. It counts as met once every one is accepted. The project
        itself is not changed.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="set-outcome-deliverables-form"
        initialValues={{
          deliverableIds: outcome.deliverables.map((d) => d.id),
        }}
      >
        <Item name="deliverableIds" label="Deliverables">
          <OutcomeDeliverablesSelect
            requestId={requestId}
            projectId={outcome.targetId}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default SetOutcomeDeliverablesForm
