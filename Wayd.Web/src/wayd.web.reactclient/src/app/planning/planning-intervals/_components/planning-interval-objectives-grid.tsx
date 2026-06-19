'use client'

import { PlanningIntervalObjectiveListDto } from '@/src/services/wayd-api'
import Link from 'next/link'
import { useMemo, useState } from 'react'
import WaydGrid from '../../../../components/common/wayd-grid'
import { MenuProps, Progress } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import useAuth from '../../../../components/contexts/auth'
import EditPlanningIntervalObjectiveForm from '@/src/app/planning/planning-intervals/_components/edit-planning-interval-objective-form'
import CreateHealthCheckForm from './create-pi-objective-health-check-form'
import {
  NestedPiObjectiveHealthCheckStatusCellRenderer,
  PlanningIntervalObjectiveLinkCellRenderer,
  RowMenuCellRenderer,
  NestedTeamNameLinkCellRenderer,
  NestedPlanningIntervalLinkCellRenderer,
} from '../../../../components/common/wayd-grid-cell-renderers'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import { ControlItemSwitch } from '../../../../components/common/control-items-menu'

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

const ProgressCellRenderer = ({ value, data }: ICellRendererParams<PlanningIntervalObjectiveListDto>) => {
  const progressStatus = ['Canceled', 'Missed'].includes(data?.status?.name ?? '')
    ? 'exception'
    : undefined
  return <Progress percent={value} size="small" status={progressStatus} />
}

interface RowMenuProps extends MenuProps {
  planningIntervalId: string
  planningIntervalKey: number
  objectiveId: string
  objectiveKey: number
  canManageObjectives: boolean
  canCreateHealthChecks: boolean
  onEditObjectiveMenuClicked: (id: string, key: number) => void
  onCreateHealthCheckMenuClicked: (
    planningIntervalId: string,
    id: string,
  ) => void
}

const getRowMenuItems = (props: RowMenuProps) => {
  if (
    (!props.canManageObjectives && !props.canCreateHealthChecks) ||
    !props.objectiveId ||
    !props.onEditObjectiveMenuClicked ||
    !props.onCreateHealthCheckMenuClicked
  ) {
    return null
  }
  return [
    {
      key: 'editObjective',
      label: 'Edit Objective',
      disabled: !props.canManageObjectives,
      onClick: () =>
        props.onEditObjectiveMenuClicked(props.objectiveId, props.objectiveKey),
    },
    {
      key: 'createHealthCheck',
      label: 'Create Health Check',
      disabled: !props.canCreateHealthChecks,
      onClick: () =>
        props.onCreateHealthCheckMenuClicked(
          props.planningIntervalId,
          props.objectiveId,
        ),
    },
    {
      key: 'healthReport',
      label: (
        <Link
          href={`/planning/planning-intervals/${props.planningIntervalKey}/objectives/${props.objectiveKey}/health-report`}
        >
          Health Report
        </Link>
      ),
    },
  ] as ItemType[]
}

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

  const columnDefs = useMemo<ColDef<PlanningIntervalObjectiveListDto>[]>(() => {
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
      {
        width: 50,
        filter: false,
        sortable: false,
        resizable: false,
        hide: !canManageObjectives,
        cellRenderer: (params: ICellRendererParams<PlanningIntervalObjectiveListDto>) => {
          if (!params.data) return null
          const menuItems = getRowMenuItems({
            planningIntervalId: params.data.planningInterval.id,
            planningIntervalKey: planningIntervalKey,
            objectiveId: params.data.id,
            objectiveKey: params.data.key,
            canManageObjectives,
            canCreateHealthChecks,
            onEditObjectiveMenuClicked,
            onCreateHealthCheckMenuClicked,
          })

          return RowMenuCellRenderer({ ...params, menuItems: menuItems ?? [] })
        },
      },
      { field: 'id', hide: true },
      { field: 'key', width: 90 },
      {
        field: 'name',
        width: 500,
        cellRenderer: PlanningIntervalObjectiveLinkCellRenderer,
      },
      { field: 'isStretch', width: 100 },
      {
        field: 'planningInterval.name',
        headerName: 'Planning Interval',
        cellRenderer: NestedPlanningIntervalLinkCellRenderer,
        hide: hidePlanningInterval,
      },
      { field: 'status.name', headerName: 'Status', width: 125 },
      {
        field: 'team.name',
        headerName: 'Team',
        cellRenderer: NestedTeamNameLinkCellRenderer,
        hide: hideTeam,
      },
      {
        field: 'healthCheck.status.name',
        headerName: 'Health',
        width: 125,
        cellRenderer: NestedPiObjectiveHealthCheckStatusCellRenderer,
      },
      { field: 'progress', width: 250, cellRenderer: ProgressCellRenderer },
      {
        field: 'startDate',
        type: 'dateOnly',
      },
      {
        field: 'targetDate',
        type: 'dateOnly',
      },
      {
        field: 'order',
        width: 100,
        comparator: (a, b) => {
          if (!a) return 1 // sort empty at the end
          if (!b) return -1

          return a - b
        },
      },
    ]
  }, [
    planningIntervalKey,
    canManageObjectives,
    canCreateHealthChecks,
    hidePlanningInterval,
    hideTeam,
  ])

  const onHidePlanningIntervalChange = (checked: boolean) => {
    setHidePlanningInterval(checked)
  }

  const onHideTeamChange = (checked: boolean) => {
    setHideTeam(checked)
  }

  const controlItems = (): ItemType[] => {
    const items: ItemType[] = []

    items.push(
      {
        label: (
          <ControlItemSwitch
            label="Hide PI"
            checked={hidePlanningInterval}
            onChange={onHidePlanningIntervalChange}
          />
        ),
        key: 'hide-planning-interval',
        onClick: () => onHidePlanningIntervalChange(!hidePlanningInterval),
      },
      {
        label: (
          <ControlItemSwitch
            label="Hide Team"
            checked={hideTeam}
            onChange={onHideTeamChange}
          />
        ),
        key: 'hide-team',
        onClick: () => onHideTeamChange(!hideTeam),
      },
    )

    return items
  }

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
      {/* TODO:  setup dynamic height */}
      <WaydGrid
        height={650}
        columnDefs={columnDefs}
        rowData={objectivesData}
        loading={isLoading}
        loadData={refresh}
        gridControlMenuItems={controlItems()}
        toolbarActions={viewSelector}
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
