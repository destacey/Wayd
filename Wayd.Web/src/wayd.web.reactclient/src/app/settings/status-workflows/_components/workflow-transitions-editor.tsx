'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  StatusWorkflowDetailsDto,
  TransitionMode,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import {
  useSetWorkflowTransitionModeMutation,
  useSetWorkflowTransitionsMutation,
} from '@/src/store/features/common/status-workflows-api'
import { isApiError } from '@/src/utils'
import { Alert, Checkbox, Flex, Segmented, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'

const { Text } = Typography

export interface WorkflowTransitionsEditorProps {
  statusWorkflow: StatusWorkflowDetailsDto
  /** Whether the user holds the update permission. The workflow decides the rest. */
  canUpdate: boolean
}

const moveKey = (fromStatusId: string, toStatusId: string) =>
  `${fromStatusId}:${toStatusId}`

/**
 * Whether a workflow allows any move or only its transitions, and a from × to
 * grid of those transitions. Editable only while the workflow is a draft; each
 * change saves the whole set, which is what the API replaces.
 */
const WorkflowTransitionsEditor = ({
  statusWorkflow,
  canUpdate,
}: WorkflowTransitionsEditorProps) => {
  const messageApi = useMessage()
  const [setTransitionMode, { isLoading: isSavingMode }] =
    useSetWorkflowTransitionModeMutation()
  const [setTransitions, { isLoading: isSavingTransitions }] =
    useSetWorkflowTransitionsMutation()

  const isSaving = isSavingMode || isSavingTransitions
  const isEditable = canUpdate && statusWorkflow.canEdit
  const isRestricted =
    statusWorkflow.transitionMode === TransitionMode.Restricted

  const statuses = [...(statusWorkflow.statuses ?? [])].sort(
    (a, b) => a.order - b.order,
  )
  const transitions = statusWorkflow.transitions ?? []
  const allowed = new Set(
    transitions.map((t) => moveKey(t.fromStatusId, t.toStatusId)),
  )
  const issues = statusWorkflow.transitionIssues ?? []

  const reportError = (error: unknown, fallback: string) => {
    messageApi.error((isApiError(error) ? error.detail : undefined) ?? fallback)
    console.error(error)
  }

  const changeMode = async (mode: TransitionMode) => {
    const response = await setTransitionMode({
      workflowId: statusWorkflow.id,
      request: { mode },
    })
    if (response.error) {
      reportError(
        response.error,
        'An unexpected error occurred while changing the transition mode.',
      )
    }
  }

  const toggleMove = async (
    from: WorkflowStatusDto,
    to: WorkflowStatusDto,
    isAllowed: boolean,
  ) => {
    const next = isAllowed
      ? [
          ...transitions.map((t) => ({
            fromStatusId: t.fromStatusId,
            toStatusId: t.toStatusId,
          })),
          { fromStatusId: from.id, toStatusId: to.id },
        ]
      : transitions
          .filter(
            (t) => !(t.fromStatusId === from.id && t.toStatusId === to.id),
          )
          .map((t) => ({
            fromStatusId: t.fromStatusId,
            toStatusId: t.toStatusId,
          }))

    const response = await setTransitions({
      workflowId: statusWorkflow.id,
      request: { transitions: next },
    })
    if (response.error) {
      reportError(
        response.error,
        'An unexpected error occurred while saving the transitions.',
      )
    }
  }

  const columns: ColumnsType<WorkflowStatusDto> = [
    {
      key: 'from',
      title: 'From \\ To',
      fixed: 'left',
      render: (_, from) => (
        <Flex vertical>
          <Text strong>{from.name}</Text>
          <Text type="secondary">{from.category.name}</Text>
        </Flex>
      ),
    },
    ...statuses.map((to) => ({
      key: to.id,
      title: to.name,
      align: 'center' as const,
      render: (_: unknown, from: WorkflowStatusDto) =>
        from.id === to.id ? (
          <Text type="secondary">—</Text>
        ) : (
          <Checkbox
            aria-label={`${from.name} to ${to.name}`}
            checked={allowed.has(moveKey(from.id, to.id))}
            disabled={!isEditable || isSaving}
            onChange={(e) => toggleMove(from, to, e.target.checked)}
          />
        ),
    })),
  ]

  return (
    <Flex vertical gap={12}>
      <Flex align="center" gap={12} wrap>
        <Segmented<TransitionMode>
          value={statusWorkflow.transitionMode}
          disabled={!isEditable || isSaving}
          onChange={changeMode}
          options={[
            { label: 'Any move', value: TransitionMode.AnyToAny },
            {
              label: 'Only allowed moves',
              value: TransitionMode.Restricted,
              disabled: !statusWorkflow.supportsRestrictedTransitions,
            },
          ]}
        />
        {!statusWorkflow.supportsRestrictedTransitions && (
          <Text type="secondary">
            {statusWorkflow.owner.name} workflows always allow any move.
          </Text>
        )}
      </Flex>

      {isRestricted ? (
        <>
          <Text type="secondary">
            A record can only move from a status in the left column to a status
            checked in its row.
            {!isEditable &&
              ' Clone this workflow into a draft to change its transitions.'}
          </Text>
          {issues.length > 0 && (
            <Alert
              type="warning"
              showIcon
              title="This workflow cannot be published yet"
              description={
                <ul style={{ marginTop: 0, marginBottom: 0 }}>
                  {issues.map((issue) => (
                    <li key={issue}>{issue}</li>
                  ))}
                </ul>
              }
            />
          )}
          <Table<WorkflowStatusDto>
            rowKey="id"
            size="small"
            columns={columns}
            dataSource={statuses}
            pagination={false}
            scroll={{ x: 'max-content' }}
          />
        </>
      ) : (
        <Alert
          type="info"
          showIcon
          title="This workflow allows any move"
          description="A record can move from any status to any other."
        />
      )}
    </Flex>
  )
}

export default WorkflowTransitionsEditor
