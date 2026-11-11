'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeRequestOutcomeDto } from '@/src/services/wayd-api'
import { useSetIntakeRequestOutcomeDeliverablesMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
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
 * outcome's whole mapping with the mapping it opened on, so a change another agent made meanwhile is refused
 * rather than undone.
 */
const SetOutcomeDeliverablesForm: FC<SetOutcomeDeliverablesFormProps> = ({
  requestId,
  outcome,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [setDeliverables] = useSetIntakeRequestOutcomeDeliverablesMutation()
  // A deliverable deleted from the project stays mapped but no longer says what the project took on.
  const deletedIds = new Set(
    outcome.deliverables.filter((d) => !d.isOnProject).map((d) => d.id),
  )

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<SetOutcomeDeliverablesFormValues>({
      onSubmit: async (values, form) => {
        try {
          await setDeliverables({
            requestId,
            targetId: outcome.targetId,
            deliverableIds: values.deliverableIds ?? [],
            expectedDeliverableIds: outcome.deliverables.map((d) => d.id),
          }).unwrap()
          messageApi.success('Deliverables updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 409) {
            // Another change landed first; the request reloads, and closing lets the agent start from it.
            messageApi.error(
              apiError.detail ??
                'The deliverables changed since you opened this. Open it again to see them.',
            )
            return true
          }
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to update the deliverables.',
            )
          }
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
        <Item
          name="deliverableIds"
          label="Deliverables"
          required={!outcome.note}
          rules={
            outcome.note
              ? undefined
              : [
                  {
                    validator: async (_, ids: string[] | undefined) => {
                      if (!(ids ?? []).some((id) => !deletedIds.has(id)))
                        throw new Error()
                    },
                    message:
                      'This outcome has no note, so it needs at least one deliverable still on the project. To clear them, remove the outcome and add it again with a note.',
                  },
                ]
          }
        >
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
