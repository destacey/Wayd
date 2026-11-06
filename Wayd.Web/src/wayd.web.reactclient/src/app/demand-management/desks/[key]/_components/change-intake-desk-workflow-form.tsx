'use client'

import StatusRemapTable from '@/src/app/settings/status-workflows/_components/status-remap-table'
import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeDeskWorkflowDto } from '@/src/services/wayd-api'
import {
  useChangeIntakeDeskWorkflowMutation,
  useGetIntakeWorkflowChoicesQuery,
  usePreviewIntakeDeskWorkflowChangeQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import {
  Alert,
  Descriptions,
  Flex,
  Modal,
  Select,
  Spin,
  Steps,
  Typography,
} from 'antd'
import { FC, useState } from 'react'

const { Paragraph, Text } = Typography

export interface ChangeIntakeDeskWorkflowFormProps {
  deskId: string
  deskName: string
  workflow: IntakeDeskWorkflowDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const requestCount = (count: number) =>
  `${count.toLocaleString()} request${count === 1 ? '' : 's'}`

type Stage = 'workflow' | 'mapping' | 'confirm'

/**
 * Moves a desk and every request on it onto another published intake workflow. Stepped like the
 * reassignment in Settings: the mapping of each status, with the requests in it, is reviewed before
 * anything moves; a desk with no request skips it, having nothing to map. Offered where the desk
 * workflow's `canChange` says so.
 */
const ChangeIntakeDeskWorkflowForm: FC<ChangeIntakeDeskWorkflowFormProps> = ({
  deskId,
  deskName,
  workflow,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()

  const [stage, setStage] = useState<Stage>('workflow')
  const [targetId, setTargetId] = useState<string | undefined>()
  const [decisions, setDecisions] = useState<Record<string, string>>({})
  const [refusal, setRefusal] = useState<string | undefined>()

  const [changeWorkflow, { isLoading: isSaving }] =
    useChangeIntakeDeskWorkflowMutation()
  const { data: choices, isLoading: isLoadingChoices } =
    useGetIntakeWorkflowChoicesQuery()

  const {
    data: preview,
    isFetching: isPreviewLoading,
    error: previewError,
    refetch: refetchPreview,
  } = usePreviewIntakeDeskWorkflowChangeQuery(
    { deskId, workflowId: targetId! },
    { skip: !targetId },
  )

  // Re-seeded per preview. A refreshed preview of the same target keeps the operator's choices; another
  // target starts from its own automatic mapping, so stale choices cannot carry into a different workflow.
  const [seededPreview, setSeededPreview] = useState<typeof preview>()
  if (preview !== seededPreview) {
    const sameTarget = seededPreview?.remap.to.id === preview?.remap.to.id
    setSeededPreview(preview)
    if (preview) {
      setDecisions((current) =>
        Object.fromEntries(
          preview.remap.entries.flatMap((entry) => {
            const to =
              (sameTarget ? current[entry.from.id] : undefined) ?? entry.to?.id
            return to ? [[entry.from.id, to]] : []
          }),
        ),
      )
    }
  }

  const candidates = (choices ?? []).filter(
    (w) => w.id !== workflow.workflow.id,
  )
  const affected = preview?.remap.affectedRecordCount ?? 0
  const isComplete =
    preview !== undefined &&
    preview.remap.entries.every((entry) => decisions[entry.from.id])

  const stages: Stage[] =
    affected > 0 ? ['workflow', 'mapping', 'confirm'] : ['workflow', 'confirm']
  const current = Math.max(stages.indexOf(stage), 0)

  const onTargetChange = (id: string) => {
    setTargetId(id)
    setRefusal(undefined)
  }

  const onSubmit = async () => {
    setRefusal(undefined)
    try {
      const moved = await changeWorkflow({
        deskId,
        workflowId: targetId!,
        decisions: Object.entries(decisions).map(
          ([fromStatusId, toStatusId]) => ({ fromStatusId, toStatusId }),
        ),
      }).unwrap()
      messageApi.success(
        moved > 0
          ? `Workflow changed; ${requestCount(moved)} moved.`
          : 'Workflow changed.',
      )
      onFormComplete()
    } catch (error) {
      // Back to the mapping with the choices kept and the counts refreshed: the likely refusal names the
      // requests that cannot land where they are mapped, and requests may have moved since the preview.
      setRefusal(toApiError(error).detail ?? 'Failed to change the workflow.')
      refetchPreview()
      setStage(affected > 0 ? 'mapping' : 'workflow')
    }
  }

  const okDisabled =
    (stage === 'workflow' && (!targetId || isPreviewLoading || !preview)) ||
    (stage === 'mapping' && (isPreviewLoading || !isComplete))

  const refusalAlert = refusal && (
    <Alert type="error" showIcon title={refusal} />
  )

  return (
    <Modal
      title="Change Workflow"
      open
      width={stage === 'workflow' ? 520 : 760}
      okText={stage === 'confirm' ? 'Change' : 'Next'}
      okButtonProps={{
        disabled: okDisabled,
        danger: stage === 'confirm' && affected > 0,
      }}
      confirmLoading={isSaving}
      onOk={() =>
        stage === 'confirm' ? onSubmit() : setStage(stages[current + 1])
      }
      onCancel={() =>
        stage === 'workflow' ? onFormCancel() : setStage(stages[current - 1])
      }
      cancelText={stage === 'workflow' ? 'Cancel' : 'Back'}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={16}>
        <Steps
          size="small"
          current={current}
          items={stages.map((s) => ({
            title:
              s === 'workflow'
                ? 'Workflow'
                : s === 'mapping'
                  ? 'Mapping'
                  : 'Confirm',
          }))}
        />

        {stage === 'workflow' && (
          <Flex vertical gap={12}>
            <Paragraph type="secondary" style={{ margin: 0 }}>
              The stages {deskName} reviews its requests in. Choosing a workflow
              other than the organization&apos;s default gives the desk its own,
              so a later change to the default no longer reaches it. Workflows
              are built in Settings, under Status Workflows.
            </Paragraph>
            <Descriptions size="small" column={1}>
              <Descriptions.Item label="Currently using">
                {workflow.workflow.name}
              </Descriptions.Item>
            </Descriptions>
            <Select
              aria-label="Workflow"
              value={targetId}
              onChange={onTargetChange}
              loading={isLoadingChoices}
              placeholder="Choose a workflow to move to"
              options={candidates.map((w) => ({ value: w.id, label: w.name }))}
              notFoundContent="No other published intake workflow"
            />
            {isPreviewLoading && <Spin size="small" />}
            {!!previewError && (
              <Alert
                type="error"
                showIcon
                title={
                  toApiError(previewError).detail ??
                  'The change could not be previewed.'
                }
              />
            )}
            {preview && !isPreviewLoading && (
              <Alert
                type="info"
                showIcon
                title={
                  affected > 0
                    ? `${requestCount(affected)} on the desk will move to a status in ${preview.remap.to.name}.`
                    : 'No request is on the desk, so none will move.'
                }
                description="Each move is recorded in the request's status history."
              />
            )}
            {refusalAlert}
          </Flex>
        )}

        {stage === 'mapping' && preview && (
          <Flex vertical gap={8}>
            {refusalAlert}
            <Text type="secondary">
              Choose where requests in each status go. A request lands in a
              Duplicate status only while it names the request it duplicates,
              and in a closing status that needs a reason only while it has one.
            </Text>
            <StatusRemapTable
              entries={preview.remap.entries}
              targetStatuses={preview.targetStatuses}
              decisions={decisions}
              onChange={(fromStatusId, toStatusId) =>
                setDecisions((current) => ({
                  ...current,
                  [fromStatusId]: toStatusId,
                }))
              }
            />
          </Flex>
        )}

        {stage === 'confirm' && preview && (
          <Alert
            type={affected > 0 ? 'warning' : 'info'}
            showIcon
            title={
              affected > 0
                ? `${requestCount(affected)} will move.`
                : 'No request will move.'
            }
            description={
              <Text>
                {deskName} moves from{' '}
                <Text strong>{preview.remap.from.name}</Text> to{' '}
                <Text strong>{preview.remap.to.name}</Text>. Moving back is
                another change.
              </Text>
            }
          />
        )}
      </Flex>
    </Modal>
  )
}

export default ChangeIntakeDeskWorkflowForm
