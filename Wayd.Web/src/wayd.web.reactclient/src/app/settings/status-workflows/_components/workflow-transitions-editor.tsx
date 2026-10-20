'use client'

import { transitionConditionLabels } from '@/src/components/common/status-workflows'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  StatusWorkflowDetailsDto,
  TransitionConditionKind,
  TransitionMode,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import {
  useSetWorkflowTransitionModeMutation,
  useSetWorkflowTransitionsMutation,
} from '@/src/store/features/common/status-workflows-api'
import { isApiError } from '@/src/utils'
import { AuditOutlined } from '@ant-design/icons'
import {
  Alert,
  Button,
  Checkbox,
  Flex,
  Popover,
  Segmented,
  Table,
  Tooltip,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useState } from 'react'

const { Text } = Typography

export interface WorkflowTransitionsEditorProps {
  statusWorkflow: StatusWorkflowDetailsDto
  /** Whether the user holds the update permission. The workflow decides the rest. */
  canUpdate: boolean
}

interface Move {
  fromStatusId: string
  toStatusId: string
  conditions: TransitionConditionKind[]
}

const moveKey = (fromStatusId: string, toStatusId: string) =>
  `${fromStatusId}:${toStatusId}`

const setKey = (moves: Move[]) =>
  moves
    .map(
      (m) =>
        `${moveKey(m.fromStatusId, m.toStatusId)}:${[...m.conditions].sort().join(',')}`,
    )
    .sort()
    .join('|')

/**
 * Whether a workflow allows any move or only its transitions, and a from × to
 * grid of those transitions with the conditions each carries. Editable only
 * while the workflow is a draft; each change saves the whole set, which is what
 * the API replaces.
 *
 * The grid works from its own copy of the set, conditions included, updated as
 * each edit is made: the next edit can land before the workflow is refetched,
 * and a set built from the stale prop would silently undo the edit before it.
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
  const savedMoves: Move[] = (statusWorkflow.transitions ?? []).map((t) => ({
    fromStatusId: t.fromStatusId,
    toStatusId: t.toStatusId,
    conditions: [...(t.conditions ?? [])],
  }))

  // Re-seeded whenever the saved set changes, so a refetch, a clone or an edit
  // elsewhere replaces the local copy (React's adjust-state-on-prop-change pattern).
  const [moves, setMoves] = useState<Move[]>(savedMoves)
  const [seededFrom, setSeededFrom] = useState(setKey(savedMoves))
  if (seededFrom !== setKey(savedMoves)) {
    setSeededFrom(setKey(savedMoves))
    setMoves(savedMoves)
  }

  const movesByKey = new Map(
    moves.map((m) => [moveKey(m.fromStatusId, m.toStatusId), m]),
  )
  const issues = statusWorkflow.transitionIssues ?? []
  const availableConditions = statusWorkflow.availableConditions ?? []

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

  const save = async (next: Move[]) => {
    const previous = moves
    setMoves(next)

    const response = await setTransitions({
      workflowId: statusWorkflow.id,
      request: { transitions: next },
    })
    if (response.error) {
      setMoves(previous)
      reportError(
        response.error,
        'An unexpected error occurred while saving the transitions.',
      )
    }
  }

  const toggleMove = (
    from: WorkflowStatusDto,
    to: WorkflowStatusDto,
    isAllowed: boolean,
  ) =>
    save(
      isAllowed
        ? [
            ...moves,
            { fromStatusId: from.id, toStatusId: to.id, conditions: [] },
          ]
        : moves.filter(
            (m) => !(m.fromStatusId === from.id && m.toStatusId === to.id),
          ),
    )

  const changeConditions = (
    move: Move,
    conditions: TransitionConditionKind[],
  ) =>
    save(
      moves.map((m) =>
        m.fromStatusId === move.fromStatusId && m.toStatusId === move.toStatusId
          ? { ...m, conditions }
          : m,
      ),
    )

  const renderConditions = (
    move: Move,
    from: WorkflowStatusDto,
    to: WorkflowStatusDto,
  ) => {
    const hasConditions = move.conditions.length > 0
    if (!isEditable && !hasConditions) return null

    const label = `Conditions on ${from.name} to ${to.name}`
    const summary = hasConditions
      ? move.conditions.map((c) => transitionConditionLabels[c]).join(', ')
      : 'No conditions'

    // A condition the move already holds stays listed even when it can no
    // longer be added, so it can still be removed.
    const kinds = [
      ...availableConditions,
      ...move.conditions.filter((c) => !availableConditions.includes(c)),
    ]

    return (
      <Popover
        trigger="click"
        title={`${from.name} → ${to.name}`}
        content={
          <Checkbox.Group
            aria-label={label}
            value={move.conditions}
            disabled={!isEditable || isSaving}
            onChange={(values) => changeConditions(move, values)}
          >
            <Flex vertical gap={4}>
              {kinds.map((kind) => (
                <Checkbox
                  key={kind}
                  value={kind}
                  disabled={
                    !availableConditions.includes(kind) &&
                    !move.conditions.includes(kind)
                  }
                >
                  {transitionConditionLabels[kind]}
                </Checkbox>
              ))}
            </Flex>
          </Checkbox.Group>
        }
      >
        <Tooltip title={summary}>
          <Button
            size="small"
            aria-label={label}
            color={hasConditions ? 'primary' : 'default'}
            variant={hasConditions ? 'filled' : 'text'}
            icon={<AuditOutlined />}
          />
        </Tooltip>
      </Popover>
    )
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
      render: (_: unknown, from: WorkflowStatusDto) => {
        if (from.id === to.id) return <Text type="secondary">—</Text>

        const move = movesByKey.get(moveKey(from.id, to.id))
        return (
          <Flex align="center" justify="center" gap={4}>
            <Checkbox
              aria-label={`${from.name} to ${to.name}`}
              checked={move !== undefined}
              disabled={!isEditable || isSaving}
              onChange={(e) => toggleMove(from, to, e.target.checked)}
            />
            {move && renderConditions(move, from, to)}
          </Flex>
        )
      },
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
            checked in its row. A highlighted <AuditOutlined /> marks a move
            with conditions, such as requiring a reason.
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
