'use client'

import {
  IntakeDeskDto,
  IntakeDeskWorkflowDto,
  IntakeRequestSummaryDto,
} from '@/src/services/wayd-api'
import { useGetIntakeDeskWorkflowsQuery } from '@/src/store/features/demand-management/demand-management-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { Alert, Button, Flex, Skeleton, Typography } from 'antd'
import { FC } from 'react'
import RequestBoard, { boardColumns } from './request-board'
import { RequestFilters } from './request-filters'

const { Text } = Typography

/** The desks the board covers: those the filter names, or every desk when it names none. */
export const boardDeskIds = (
  filters: RequestFilters,
  desks: IntakeDeskDto[] | undefined,
): string[] =>
  [
    ...(filters.deskIds.length > 0
      ? filters.deskIds
      : (desks ?? []).map((d) => d.id)),
  ].sort()

/** The one workflow the desks follow, or null when they follow different ones. */
export const sharedWorkflow = (
  workflows: IntakeDeskWorkflowDto[],
): IntakeDeskWorkflowDto | null =>
  workflows.length > 0 &&
  workflows.every((w) => w.workflow.id === workflows[0].workflow.id)
    ? workflows[0]
    : null

/**
 * Which desks the board is for, always in words, so nobody reads one desk's stages as another's:
 * "The Product delivery desk", or "All desks, which share one workflow: Intake review".
 */
export const boardTitle = (
  filters: RequestFilters,
  desks: IntakeDeskDto[] | undefined,
  workflowName: string,
): string => {
  if (filters.deskIds.length === 0)
    return `All desks, which share one workflow: ${workflowName}`
  const names = filters.deskIds
    .map((id) => desks?.find((d) => d.id === id)?.name)
    .filter((n): n is string => !!n)
    .sort(caseInsensitiveCompare)
  return names.length === 1
    ? `The ${names[0]} desk`
    : `${names.join(', ')}, which share one workflow: ${workflowName}`
}

export interface RequestBoardViewProps {
  requests: IntakeRequestSummaryDto[]
  filters: RequestFilters
  desks: IntakeDeskDto[] | undefined
  /** Narrows the filters to one desk, offered when the desks in scope have no single board. */
  onPickDesk: (deskId: string) => void
}

/**
 * The board, once the desks in scope are known to share a workflow: its columns are that workflow's stages.
 * Desks on different workflows have no single set of stages, and status names are no guide to what a stage
 * means, so the board asks for one desk rather than guess.
 */
const RequestBoardView: FC<RequestBoardViewProps> = ({
  requests,
  filters,
  desks,
  onPickDesk,
}) => {
  const deskIds = boardDeskIds(filters, desks)
  const {
    data: workflows,
    isLoading,
    isError,
  } = useGetIntakeDeskWorkflowsQuery(deskIds, { skip: deskIds.length === 0 })

  if (deskIds.length === 0)
    return (
      <Alert
        type="info"
        showIcon
        title="There are no desks to show a board for."
      />
    )
  if (isLoading || !workflows) {
    if (isError)
      return (
        <Alert
          type="error"
          showIcon
          title="The desks' workflows could not be loaded, so the board cannot be drawn."
        />
      )
    return <Skeleton active paragraph={{ rows: 6 }} />
  }

  const shared = sharedWorkflow(workflows)
  if (!shared) {
    const inScope = (desks ?? [])
      .filter((d) => deskIds.includes(d.id))
      .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
    return (
      <Alert
        type="info"
        showIcon
        title="These desks review on different workflows, so they have no single board."
        description={
          <Flex vertical gap={8}>
            <Text>Pick a desk to see its board.</Text>
            <Flex wrap gap={8}>
              {inScope.map((d) => (
                <Button key={d.id} onClick={() => onPickDesk(d.id)}>
                  {d.name}
                </Button>
              ))}
            </Flex>
          </Flex>
        }
      />
    )
  }

  return (
    <Flex vertical gap={8}>
      <Text strong>{boardTitle(filters, desks, shared.workflow.name)}</Text>
      <RequestBoard
        columns={boardColumns(shared.statuses, requests, filters.scope)}
      />
    </Flex>
  )
}

export default RequestBoardView
