'use client'

import { PlanningIntervalObjectiveListDto } from '@/src/services/wayd-api'
import Link from 'next/link'
import { useMemo, useState } from 'react'
import {
  WaydGrid2,
  createActionsColumn,
  renderTeamLink,
  renderPlanningIntervalLink,
} from '@/src/components/common/wayd-grid2'
import type { ColumnDef } from '@tanstack/react-table'
import { Progress } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import useAuth from '../../../../components/contexts/auth'
import EditPlanningIntervalObjectiveForm from '@/src/app/planning/planning-intervals/_components/edit-planning-interval-objective-form'
import CreateHealthCheckForm from './create-pi-objective-health-check-form'
import PiObjectiveHealthCheckTag from './pi-objective-health-check-tag'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '../../../../components/common/control-items-menu'

export interface PlanningIntervalObjectivesGridProps {
  objectivesData: PlanningIntervalObjectiveListDto[]
  refreshObjectives: () => void
  isLoading: boolean
  planningIntervalKey: number
  hidePlanningIntervalColumn?: boolean
  hideTeamColumn?: boolean
  viewSelector?: React.ReactNode
}

interface SelectedObjective {
  id: string
  key: number
}

/**
 * PI Objectives grid on WaydGrid2 (TanStack). Uses nested-field accessors,
 * custom cell renderers (link, progress, health tag), the dateOnly column
 * type, the default empties-last sort, and hideable columns.
 */
const PlanningIntervalObjectivesGrid = ({
  objectivesData,
  refreshObjectives,
  isLoading,
  planningIntervalKey,
  hidePlanningIntervalColumn = false,
  hideTeamColumn = false,
  viewSelector,
}: PlanningIntervalObjectivesGridProps) => {
  const [hidePlanningInterval, setHidePlanningInterval] = useState<boolean>(
    hidePlanningIntervalColumn,
  )
  const [hideTeam, setHideTeam] = useState<boolean>(hideTeamColumn)
  const [openUpdateObjectiveForm, setOpenUpdateObjectiveForm] =
    useState<boolean>(false)
  const [selectedObjective, setSelectedObjective] =
    useState<SelectedObjective | null>(null)
  const [creatingHealthCheckFor, setCreatingHealthCheckFor] = useState<{
    planningIntervalId: string
    objectiveId: string
  } | null>(null)

  const { hasPermissionClaim } = useAuth()
  const canManageObjectives = hasPermissionClaim(
    'Permissions.PlanningIntervalObjectives.Manage',
  )
  const canCreateHealthChecks = !!canManageObjectives

  const refresh = async () => {
    refreshObjectives()
  }

  const columns = useMemo<
    ColumnDef<PlanningIntervalObjectiveListDto, any>[]
  >(() => {
    const onEditObjectiveMenuClicked = (id: string, key: number) => {
      setSelectedObjective({ id, key })
      setOpenUpdateObjectiveForm(true)
    }

    const onCreateHealthCheckMenuClicked = (
      planningIntervalId: string,
      id: string,
    ) => {
      setCreatingHealthCheckFor({ planningIntervalId, objectiveId: id })
    }

    return [
      createActionsColumn<PlanningIntervalObjectiveListDto>({
        hide: !canManageObjectives,
        ariaLabel: 'Objective actions',
        getItems: (obj) => [
          {
            key: 'editObjective',
            label: 'Edit Objective',
            disabled: !canManageObjectives,
            onClick: () => onEditObjectiveMenuClicked(obj.id, obj.key),
          },
          {
            key: 'createHealthCheck',
            label: 'Create Health Check',
            disabled: !canCreateHealthChecks,
            onClick: () =>
              onCreateHealthCheckMenuClicked(obj.planningInterval.id, obj.id),
          },
          {
            key: 'healthReport',
            label: (
              <Link
                href={`/planning/planning-intervals/${planningIntervalKey}/objectives/${obj.key}/health-report`}
              >
                Health Report
              </Link>
            ),
          },
        ],
      }),
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 500,
        cell: ({ row }) => (
          <Link
            href={`/planning/planning-intervals/${row.original.planningInterval?.key}/objectives/${row.original.key}`}
          >
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'isStretch',
        accessorKey: 'isStretch',
        header: 'Stretch',
        meta: { columnType: 'yesNo' },
      },
      {
        id: 'planningInterval',
        accessorKey: 'planningInterval.name',
        header: 'Planning Interval',
        meta: { hide: hidePlanningInterval },
        cell: ({ row }) =>
          renderPlanningIntervalLink(row.original.planningInterval),
      },
      {
        id: 'status',
        accessorKey: 'status.name',
        header: 'Status',
        size: 125,
        meta: { filterType: 'set' },
      },
      {
        id: 'team',
        accessorKey: 'team.name',
        header: 'Team',
        meta: { hide: hideTeam, filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.team),
      },
      {
        id: 'health',
        accessorFn: (row) => row.healthCheck?.status?.name ?? '',
        header: 'Health',
        size: 125,
        cell: ({ row }) => {
          const obj = row.original
          if (!obj.healthCheck) return null
          return (
            <PiObjectiveHealthCheckTag
              healthCheck={obj.healthCheck}
              planningIntervalId={obj.planningInterval?.id}
              objectiveId={obj.id}
            />
          )
        },
      },
      {
        id: 'progress',
        accessorKey: 'progress',
        header: 'Progress',
        size: 250,
        enableColumnFilter: false,
        cell: ({ row }) => {
          const obj = row.original
          const status = ['Canceled', 'Missed'].includes(obj.status?.name ?? '')
            ? 'exception'
            : undefined
          return (
            <Progress percent={obj.progress} size="small" status={status} />
          )
        },
      },
      {
        id: 'startDate',
        accessorKey: 'startDate',
        header: 'Start Date',
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'targetDate',
        accessorKey: 'targetDate',
        header: 'Target Date',
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'order',
        accessorKey: 'order',
        header: 'Order',
        size: 100,
        enableColumnFilter: false,
        // Empties sort last (asc) via the grid's default sortEmptyLast sortingFn.
      },
    ]
  }, [
    planningIntervalKey,
    canManageObjectives,
    canCreateHealthChecks,
    hidePlanningInterval,
    hideTeam,
  ])

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Hide PI"
          checked={hidePlanningInterval}
          onChange={setHidePlanningInterval}
        />
      ),
      key: 'hide-planning-interval',
      onClick: () => setHidePlanningInterval((prev) => !prev),
    },
    {
      label: (
        <ControlItemSwitch
          label="Hide Team"
          checked={hideTeam}
          onChange={setHideTeam}
        />
      ),
      key: 'hide-team',
      onClick: () => setHideTeam((prev) => !prev),
    },
  ]

  const onEditObjectiveFormClosed = (wasSaved: boolean) => {
    setOpenUpdateObjectiveForm(false)
    setSelectedObjective(null)
    if (wasSaved) {
      refresh()
    }
  }

  const onCreateHealthCheckFormClosed = (wasSaved: boolean) => {
    setCreatingHealthCheckFor(null)
    if (wasSaved) {
      refresh()
    }
  }

  if (!planningIntervalKey) return null

  return (
    <>
      <WaydGrid2
        height={650}
        columns={columns}
        data={objectivesData}
        isLoading={isLoading}
        onRefresh={refresh}
        csvFileName="pi-objectives"
        rightSlot={
          <>
            <ControlItemsMenu items={controlItems} />
            {viewSelector}
          </>
        }
      />
      {openUpdateObjectiveForm && selectedObjective && (
        <EditPlanningIntervalObjectiveForm
          objectiveKey={selectedObjective.key}
          planningIntervalKey={planningIntervalKey}
          onFormSave={() => onEditObjectiveFormClosed(true)}
          onFormCancel={() => onEditObjectiveFormClosed(false)}
        />
      )}
      {creatingHealthCheckFor && (
        <CreateHealthCheckForm
          planningIntervalId={creatingHealthCheckFor.planningIntervalId}
          objectiveId={creatingHealthCheckFor.objectiveId}
          onFormCreate={() => onCreateHealthCheckFormClosed(true)}
          onFormCancel={() => onCreateHealthCheckFormClosed(false)}
        />
      )}
    </>
  )
}

export default PlanningIntervalObjectivesGrid
