'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  RecordTypeDto,
  StatusWorkflowState,
  WorkflowSchemeDetailsDto,
} from '@/src/services/wayd-api'
import {
  useGetStatusWorkflowQuery,
  useGetStatusWorkflowsQuery,
} from '@/src/store/features/common/status-workflows-api'
import {
  useChangeWorkflowSchemeDefaultMutation,
  usePreviewWorkflowSchemeDefaultQuery,
  usePreviewWorkflowSchemeEntryQuery,
  useSetWorkflowSchemeEntryMutation,
} from '@/src/store/features/common/workflow-schemes-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Flex, Modal, Select, Steps, Typography } from 'antd'
import { useState } from 'react'
import StatusRemapTable from '../../status-workflows/_components/status-remap-table'

const { Text } = Typography

/**
 * What is being changed: one record type's entry (an existing one, or a type
 * picked here), or the scheme's default.
 */
export type SchemeWorkflowChange =
  { kind: 'entry'; recordTypeId?: string } | { kind: 'default' }

export interface ChangeSchemeWorkflowModalProps {
  scheme: WorkflowSchemeDetailsDto
  change: SchemeWorkflowChange
  /** The owner type's record types, to pick from when adding an entry. */
  recordTypes: RecordTypeDto[]
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Stepped like a workflow reassignment: the change can move records in every
 * scope on the scheme, so the mapping is reviewed before it runs.
 */
const ChangeSchemeWorkflowModal = ({
  scheme,
  change,
  recordTypes,
  onFormComplete,
  onFormCancel,
}: ChangeSchemeWorkflowModalProps) => {
  const messageApi = useMessage()

  const [step, setStep] = useState<number>(0)
  const [recordTypeId, setRecordTypeId] = useState<string | undefined>(
    change.kind === 'entry' ? change.recordTypeId : undefined,
  )
  const [workflowId, setWorkflowId] = useState<string | undefined>()
  const [decisions, setDecisions] = useState<Record<string, string>>({})

  const [setEntry, { isLoading: isSettingEntry }] =
    useSetWorkflowSchemeEntryMutation()
  const [changeDefault, { isLoading: isChangingDefault }] =
    useChangeWorkflowSchemeDefaultMutation()

  const { data: workflows } = useGetStatusWorkflowsQuery({
    ownerType: scheme.owner.key,
    state: StatusWorkflowState.Published,
  })

  const entry = scheme.entries.find((e) => e.recordType.id === recordTypeId)
  const currentWorkflowId =
    change.kind === 'default'
      ? scheme.defaultWorkflow.id
      : (entry?.workflow.id ?? scheme.defaultWorkflow.id)

  const isEntry = change.kind === 'entry'
  const ready = !!workflowId && (!isEntry || !!recordTypeId)

  const entryPreview = usePreviewWorkflowSchemeEntryQuery(
    { schemeId: scheme.id, recordTypeId: recordTypeId!, workflowId },
    { skip: !isEntry || !ready || step === 0 },
  )
  const defaultPreview = usePreviewWorkflowSchemeDefaultQuery(
    { schemeId: scheme.id, workflowId: workflowId! },
    { skip: isEntry || !ready || step === 0 },
  )
  const { data: preview, isFetching: isPreviewLoading } = isEntry
    ? entryPreview
    : defaultPreview

  const { data: target } = useGetStatusWorkflowQuery(workflowId!, {
    skip: !workflowId,
  })

  // Re-seeded per preview, so switching workflows cannot carry stale choices
  // into a different target.
  const [seededPreview, setSeededPreview] = useState<typeof preview>()
  if (preview !== seededPreview) {
    setSeededPreview(preview)
    setDecisions(
      Object.fromEntries(
        (preview?.moves ?? []).flatMap((move) =>
          move.remap.entries
            .filter((e) => e.to)
            .map((e) => [e.from.id, e.to!.id]),
        ),
      ),
    )
  }

  const recordsMove =
    !!preview && preview.assignedScopeCount > 0 && preview.moves.length > 0
  const isComplete =
    !!preview &&
    (!recordsMove ||
      preview.moves.every((move) =>
        move.remap.entries.every((e) => decisions[e.from.id]),
      ))

  const onSubmit = async () => {
    const request = {
      decisions: Object.entries(decisions).map(
        ([fromStatusId, toStatusId]) => ({
          fromStatusId,
          toStatusId,
        }),
      ),
    }

    try {
      const response = isEntry
        ? await setEntry({
            schemeId: scheme.id,
            recordTypeId: recordTypeId!,
            request: { ...request, workflowId },
          })
        : await changeDefault({
            schemeId: scheme.id,
            request: { ...request, workflowId: workflowId! },
          })
      if (response.error) throw response.error

      messageApi.success(
        `Scheme updated. ${response.data?.toLocaleString() ?? 0} record(s) moved.`,
      )
      onFormComplete()
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(
        apiError.detail ??
          'An error occurred while changing the scheme. Please try again.',
      )
    }
  }

  const recordTypeOptions = recordTypes
    .filter((r) => r.isActive || r.id === recordTypeId)
    .map((r) => ({ value: r.id, label: r.name }))

  const workflowOptions = (workflows ?? [])
    .filter((w) => w.id !== currentWorkflowId)
    .map((w) => ({ value: w.id, label: w.name }))

  const title =
    change.kind === 'default'
      ? 'Change Default Workflow'
      : entry
        ? `Change ${entry.recordType.name} Workflow`
        : 'Add Record Type Entry'

  const okDisabled =
    (step === 0 && !ready) ||
    (step === 1 &&
      (isPreviewLoading || !preview?.canMoveRecords || !isComplete))

  return (
    <Modal
      title={title}
      open
      width={step === 0 ? 520 : 760}
      okText={step === 1 ? 'Apply' : 'Next'}
      okButtonProps={{
        disabled: okDisabled,
        danger: step === 1 && recordsMove,
      }}
      confirmLoading={isSettingEntry || isChangingDefault}
      onOk={() => (step === 1 ? onSubmit() : setStep(1))}
      onCancel={() => (step === 0 ? onFormCancel() : setStep(0))}
      cancelText={step === 0 ? 'Cancel' : 'Back'}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={16}>
        <Steps
          size="small"
          current={step}
          items={[{ title: 'Workflow' }, { title: 'Review' }]}
        />

        {step === 0 && (
          <Flex vertical gap={12}>
            {isEntry && !change.recordTypeId && (
              <Select
                aria-label="Record type"
                placeholder="Select a record type"
                value={recordTypeId}
                onChange={setRecordTypeId}
                options={recordTypeOptions}
              />
            )}
            <Text type="secondary">
              {isEntry && !entry
                ? `Follows the default now: ${scheme.defaultWorkflow.name}.`
                : `Follows now: ${entry?.workflow.name ?? scheme.defaultWorkflow.name}.`}
            </Text>
            <Select
              aria-label="Workflow"
              placeholder="Select a published workflow"
              value={workflowId}
              onChange={setWorkflowId}
              options={workflowOptions}
            />
          </Flex>
        )}

        {step === 1 && preview && (
          <Flex vertical gap={12}>
            {!recordsMove && (
              <Alert
                type="info"
                showIcon
                title={
                  preview.assignedScopeCount === 0
                    ? 'No scope follows this scheme yet, so no records move.'
                    : 'No records change workflow.'
                }
              />
            )}
            {recordsMove && !preview.canMoveRecords && (
              <Alert
                type="error"
                showIcon
                title={`${scheme.owner.name} records cannot be moved between workflows by record type yet, so this scheme cannot change while ${preview.assignedScopeCount} scope(s) follow it.`}
              />
            )}
            {recordsMove && preview.canMoveRecords && (
              <>
                <Text>
                  {`Moves ${preview.affectedRecordCount.toLocaleString()} record(s) in ${preview.assignedScopeCount} scope(s).`}
                </Text>
                {preview.moves.map((move) => (
                  <StatusRemapTable
                    key={`${move.recordType?.id ?? 'default'}-${move.remap.from.id}`}
                    entries={move.remap.entries}
                    targetStatuses={target?.statuses ?? []}
                    decisions={decisions}
                    onChange={(fromStatusId, toStatusId) =>
                      setDecisions((d) => ({
                        ...d,
                        [fromStatusId]: toStatusId,
                      }))
                    }
                  />
                ))}
              </>
            )}
          </Flex>
        )}
      </Flex>
    </Modal>
  )
}

export default ChangeSchemeWorkflowModal
