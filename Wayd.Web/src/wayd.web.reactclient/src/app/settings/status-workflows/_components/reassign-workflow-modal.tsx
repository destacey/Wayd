'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  StatusWorkflowState,
  WorkflowAssignmentDto,
} from '@/src/services/wayd-api'
import {
  useGetStatusWorkflowQuery,
  useGetStatusWorkflowsQuery,
  usePreviewStatusRemapQuery,
  useReassignWorkflowMutation,
} from '@/src/store/features/common/status-workflows-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Descriptions, Flex, Modal, Select, Steps, Typography } from 'antd'
import { useEffect, useState } from 'react'
import StatusRemapTable from './status-remap-table'

const { Text } = Typography

export interface ReassignWorkflowModalProps {
  assignment: WorkflowAssignmentDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Stepped rather than a single dialog: this rewrites every record of an owner
 * type, so the mapping is meant to be reviewed before it runs.
 */
const ReassignWorkflowModal = ({
  assignment,
  onFormComplete,
  onFormCancel,
}: ReassignWorkflowModalProps) => {
  const messageApi = useMessage()

  const [step, setStep] = useState<number>(0)
  const [targetId, setTargetId] = useState<string | undefined>()
  const [decisions, setDecisions] = useState<Record<string, string>>({})

  const [reassign, { isLoading: isSaving }] = useReassignWorkflowMutation()

  // The domain refuses a draft, a different owner type, and the current
  // workflow, so none are worth offering.
  const { data: workflows } = useGetStatusWorkflowsQuery({
    ownerType: assignment.owner.key,
    state: StatusWorkflowState.Published,
  })

  const candidates = (workflows ?? []).filter(
    (w) => w.id !== assignment.workflow.id,
  )

  const { data: preview, isFetching: isPreviewLoading } =
    usePreviewStatusRemapQuery(
      { assignmentId: assignment.id, targetWorkflowId: targetId! },
      { skip: !targetId },
    )

  const { data: target } = useGetStatusWorkflowQuery(targetId!, {
    skip: !targetId,
  })

  // Re-seeded per preview, so switching targets cannot carry stale choices into
  // a different workflow.
  useEffect(() => {
    if (!preview) return

    setDecisions(
      Object.fromEntries(
        preview.entries
          .filter((entry) => entry.to)
          .map((entry) => [entry.from.id, entry.to!.id]),
      ),
    )
  }, [preview])

  const isComplete =
    preview !== undefined &&
    preview.entries.every((entry) => decisions[entry.from.id])

  const onSubmit = async () => {
    try {
      const response = await reassign({
        assignmentId: assignment.id,
        request: {
          targetWorkflowId: targetId!,
          decisions: Object.entries(decisions).map(([fromStatusId, toStatusId]) => ({
            fromStatusId,
            toStatusId,
          })),
        },
      })

      if (response.error) throw response.error

      messageApi.success(
        `${response.data?.toLocaleString() ?? 0} record(s) moved to ${preview?.to.name}.`,
      )
      onFormComplete()
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}

      // Back to the mapping step with decisions intact: the likely failures are
      // fixable in place, and restarting would discard the whole review.
      messageApi.error(
        apiError.detail ??
          'An error occurred while reassigning the workflow. Please try again.',
      )
      setStep(1)
    }
  }

  const steps = [
    { title: 'Target' },
    { title: 'Mapping' },
    { title: 'Confirm' },
  ]

  const okText = step === 2 ? 'Reassign' : 'Next'
  const okDisabled =
    (step === 0 && !targetId) || (step === 1 && (!isComplete || isPreviewLoading))

  return (
    <Modal
      title={`Change ${assignment.owner.name} Workflow`}
      open
      width={step === 0 ? 520 : 760}
      okText={okText}
      okButtonProps={{ disabled: okDisabled, danger: step === 2 }}
      confirmLoading={isSaving}
      onOk={() => (step === 2 ? onSubmit() : setStep(step + 1))}
      onCancel={() => (step === 0 ? onFormCancel() : setStep(step - 1))}
      cancelText={step === 0 ? 'Cancel' : 'Back'}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={16}>
        <Steps size="small" current={step} items={steps} />

        {step === 0 && (
          <Flex vertical gap={12}>
            <Descriptions size="small" column={1}>
              <Descriptions.Item label="Currently using">
                {assignment.workflow.name}
              </Descriptions.Item>
            </Descriptions>
            <Select
              value={targetId}
              onChange={setTargetId}
              placeholder="Choose a workflow to move to"
              options={candidates.map((w) => ({
                value: w.id,
                label: `${w.name} (${w.statusCount} statuses)`,
              }))}
              notFoundContent="No other published workflow for this type"
            />
            <Alert
              type="info"
              showIcon
              message={`Every ${assignment.owner.name.toLowerCase()} will move to a status in the new workflow.`}
              description="Each move is recorded in the record's status history."
            />
          </Flex>
        )}

        {step === 1 && preview && target && (
          <StatusRemapTable
            entries={preview.entries}
            targetStatuses={target.statuses}
            decisions={decisions}
            onChange={(fromStatusId, toStatusId) =>
              setDecisions((current) => ({
                ...current,
                [fromStatusId]: toStatusId,
              }))
            }
          />
        )}

        {step === 2 && preview && (
          <Flex vertical gap={12}>
            <Alert
              type="warning"
              showIcon
              message={`${preview.affectedRecordCount.toLocaleString()} record(s) will move.`}
              description={
                <Text>
                  From <Text strong>{preview.from.name}</Text> to{' '}
                  <Text strong>{preview.to.name}</Text>. This cannot be undone
                  automatically — moving back is another reassignment.
                </Text>
              }
            />
          </Flex>
        )}
      </Flex>
    </Modal>
  )
}

export default ReassignWorkflowModal
