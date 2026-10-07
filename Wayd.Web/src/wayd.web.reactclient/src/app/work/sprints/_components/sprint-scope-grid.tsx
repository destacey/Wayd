'use client'

import {
  WaydGrid,
  renderAssignedToLink,
  renderSprintLink,
  renderWorkItemLink,
  renderWorkStatusTag,
  workItemKeySort,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  SizingMethod,
  SprintScopeDto,
  SprintScopeItemDto,
} from '@/src/services/wayd-api'
import { formatInstantInZone, sizingMethodLabel } from '@/src/utils'
import { Select } from 'antd'
import { useState } from 'react'
import {
  SprintScopeCategory,
  isInSprintScopeCategory,
  sprintScopeCategoryLabels,
  sprintScopeEntryLabels,
  sprintScopeOutcomeLabels,
} from './sprint-scope-categories'

export interface SprintScopeGridProps {
  scope: SprintScopeDto | undefined
  isLoading: boolean
  refetch: () => void
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

/**
 * The work items that were in a sprint's scope, as they are now, with how each
 * came in and what became of it.
 */
const SprintScopeGrid = ({
  scope,
  isLoading,
  refetch,
  persistStateKey,
}: SprintScopeGridProps) => {
  const [category, setCategory] = useState(SprintScopeCategory.All)

  const items = scope?.items ?? []
  const timeZone = scope?.timeZone ?? 'UTC'
  const sizingMethod = scope?.sizingMethod ?? SizingMethod.Count
  const countSized = sizingMethod === SizingMethod.Count
  const unit = sizingMethodLabel(sizingMethod)

  const formatInstant = (instant: Date | undefined) =>
    instant ? formatInstantInZone(instant, timeZone) : null

  const columns: ColumnDef<SprintScopeItemDto, any>[] = [
    {
      id: 'key',
      accessorKey: 'workItem.key',
      header: 'Key',
      sortFn: workItemKeySort,
      cell: ({ row }) =>
        renderWorkItemLink({
          key: row.original.workItem.key,
          workspaceKey: row.original.workItem.workspace.key,
          externalViewWorkItemUrl:
            row.original.workItem.externalViewWorkItemUrl,
        }),
    },
    {
      id: 'type',
      accessorKey: 'workItem.type',
      header: 'Type',
      size: 125,
      meta: { filterType: 'set' },
    },
    {
      id: 'title',
      accessorKey: 'workItem.title',
      header: 'Title',
      size: 400,
    },
    {
      id: 'entry',
      accessorFn: (row) => sprintScopeEntryLabels[row.entry],
      header: 'Entry',
      size: 110,
      meta: { filterType: 'set' },
    },
    {
      id: 'outcome',
      accessorFn: (row) => sprintScopeOutcomeLabels[row.outcome],
      header: 'Outcome',
      size: 170,
      meta: { filterType: 'set' },
    },
    // Under Count every estimate is 1, which says nothing a row does not.
    ...(countSized
      ? []
      : [
          {
            id: 'entryEstimate',
            accessorKey: 'entryEstimate',
            header: `${unit} In`,
            size: 110,
            meta: {
              headerTooltip: `${unit} when the item was committed or added`,
            },
          } satisfies ColumnDef<SprintScopeItemDto, any>,
          {
            id: 'outcomeEstimate',
            accessorKey: 'outcomeEstimate',
            header: `${unit} Out`,
            size: 110,
            meta: {
              headerTooltip: `${unit} when the item was last in the sprint`,
            },
          } satisfies ColumnDef<SprintScopeItemDto, any>,
        ]),
    {
      id: 'enteredAt',
      accessorFn: (row) => formatInstant(row.enteredAt),
      header: 'Added At',
      size: 190,
      meta: {
        headerTooltip: `When an added item entered the sprint, in ${timeZone}`,
      },
    },
    {
      id: 'leftAt',
      accessorFn: (row) => formatInstant(row.leftAt),
      header: 'Left At',
      size: 190,
      meta: {
        headerTooltip: `When the item last left the sprint, in ${timeZone}`,
      },
    },
    {
      id: 'status',
      accessorKey: 'workItem.status',
      header: 'Current Status',
      size: 140,
      meta: { filterType: 'set' },
      cell: ({ row }) => renderWorkStatusTag(row.original.workItem),
    },
    {
      id: 'sprint',
      accessorKey: 'workItem.sprint.name',
      header: 'Current Sprint',
      meta: { filterEnableSet: true },
      cell: ({ row }) =>
        renderSprintLink(row.original.workItem.sprint, {
          showTeamCode: false,
        }),
    },
    {
      id: 'assignedTo',
      accessorKey: 'workItem.assignedTo.name',
      header: 'Assigned To',
      meta: { filterEnableSet: true },
      cell: ({ row }) => renderAssignedToLink(row.original.workItem.assignedTo),
    },
  ]

  const categoryOptions = Object.values(SprintScopeCategory).map((value) => ({
    value,
    label: `${sprintScopeCategoryLabels[value]} (${
      items.filter((item) => isInSprintScopeCategory(item, value)).length
    })`,
  }))

  return (
    <WaydGrid
      columns={columns}
      data={items.filter((item) => isInSprintScopeCategory(item, category))}
      isLoading={isLoading}
      onRefresh={async () => {
        refetch()
      }}
      leftSlot={
        <Select
          aria-label="Scope category"
          value={category}
          options={categoryOptions}
          onChange={setCategory}
          style={{ minWidth: 180 }}
        />
      }
      persistStateKey={persistStateKey}
      csvFileName="sprint-scope"
      emptyMessage="No work items in this category"
    />
  )
}

export default SprintScopeGrid
