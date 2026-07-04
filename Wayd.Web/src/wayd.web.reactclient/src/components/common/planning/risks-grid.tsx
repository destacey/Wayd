import Link from 'next/link'
import { useMemo, useState } from 'react'
import {
  WaydGrid,
  createActionsColumn,
  renderTeamLink,
} from '../wayd-grid'
import { RiskListDto } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { Button } from 'antd'
import useAuth from '../../contexts/auth'
import CreateRiskForm from './create-risk-form'
import EditRiskForm from './edit-risk-form'
import type { ColumnDef } from '@tanstack/react-table'
import { ControlItemsMenu, ControlItemSwitch } from '../control-items-menu'

export interface RisksGridProps {
  risks: RiskListDto[]
  updateIncludeClosed: (includeClosed: boolean) => void
  teamId?: string
  isLoadingRisks: boolean
  refreshRisks: () => void
  newRisksAllowed?: boolean
  hideTeamColumn?: boolean
  gridHeight?: number
}

const RisksGrid = ({
  risks,
  updateIncludeClosed,
  teamId,
  isLoadingRisks,
  refreshRisks,
  newRisksAllowed = false,
  hideTeamColumn = false,
  gridHeight = 550,
}: RisksGridProps) => {
  const [includeClosed, setIncludeClosed] = useState<boolean>(false)
  const [hideTeam, setHideTeam] = useState<boolean>(hideTeamColumn)
  const [openCreateRiskForm, setOpenCreateRiskForm] = useState<boolean>(false)
  const [openUpdateRiskForm, setOpenUpdateRiskForm] = useState<boolean>(false)
  const [editRiskKey, setEditRiskKey] = useState<number | undefined>(undefined)

  const { hasPermissionClaim } = useAuth()
  const canCreateRisks = hasPermissionClaim('Permissions.Risks.Create')
  const canUpdateRisks = hasPermissionClaim('Permissions.Risks.Update')
  const showActions = newRisksAllowed && canCreateRisks

  const onIncludeClosedChange = (checked: boolean) => {
    setIncludeClosed(checked)
    updateIncludeClosed(checked)
  }

  const onHideTeamChange = (checked: boolean) => {
    setHideTeam(checked)
  }

  const onEditRiskFormClosed = (wasSaved: boolean) => {
    setOpenUpdateRiskForm(false)
    setEditRiskKey(undefined)
    if (wasSaved) {
      refreshRisks()
    }
  }

  const onCreateRiskFormClosed = (wasCreated: boolean) => {
    setOpenCreateRiskForm(false)
    if (wasCreated) {
      refreshRisks()
    }
  }

  const actions = () => {
    return (
      <>
        {canCreateRisks && (
          <Button type="link" onClick={() => setOpenCreateRiskForm(true)}>
            Create Risk
          </Button>
        )}
      </>
    )
  }

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Closed"
          checked={includeClosed}
          onChange={onIncludeClosedChange}
        />
      ),
      key: 'include-closed',
      onClick: () => onIncludeClosedChange(!includeClosed),
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
  ]

  const columns = useMemo<ColumnDef<RiskListDto, any>[]>(() => {
    const editRiskButtonClicked = (key: number) => {
      setEditRiskKey(key)
      setOpenUpdateRiskForm(true)
    }

    return [
    createActionsColumn<RiskListDto>({
      hide: !canUpdateRisks,
      ariaLabel: 'Risk actions',
      getItems: (risk): ItemType[] =>
        canUpdateRisks
          ? [
              {
                key: 'edit',
                label: 'Edit',
                onClick: () => editRiskButtonClicked(risk.key),
              },
            ]
          : [],
    }),
    { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
    {
      id: 'summary',
      accessorKey: 'summary',
      header: 'Summary',
      size: 300,
      meta: { filterEnableSet: true },
      cell: ({ row }) => (
        <Link href={`/planning/risks/${row.original.key}`}>
          {row.original.summary}
        </Link>
      ),
    },
    {
      id: 'team',
      accessorKey: 'team.name',
      header: 'Team',
      meta: { hide: hideTeam, filterEnableSet: true },
      cell: ({ row }) => renderTeamLink(row.original.team),
    },
    {
      id: 'status',
      accessorKey: 'status',
      header: 'Status',
      size: 125,
      meta: { hide: includeClosed === false, filterType: 'set' },
    },
    {
      id: 'category',
      accessorKey: 'category',
      header: 'Category',
      size: 125,
      meta: { filterType: 'set' },
    },
    {
      id: 'exposure',
      accessorKey: 'exposure',
      header: 'Exposure',
      size: 125,
      meta: { filterType: 'set' },
    },
    {
      id: 'followUpDate',
      accessorKey: 'followUpDate',
      header: 'Follow Up Date',
      meta: { columnType: 'dateOnly' },
    },
    {
      id: 'assignee',
      accessorKey: 'assignee.name',
      header: 'Assignee',
      cell: ({ row }) =>
        row.original.assignee ? (
          <Link
            href={`/organizations/employees/${row.original.assignee.key}`}
          >
            {row.original.assignee.name}
          </Link>
        ) : null,
    },
    {
      id: 'reportedOn',
      accessorKey: 'reportedOn',
      header: 'Reported On',
      meta: { columnType: 'dateOnly' },
    },
  ]}, [canUpdateRisks, hideTeam, includeClosed])

  return (
    <>
      <WaydGrid
        height={gridHeight}
        columns={columns}
        data={risks ?? []}
        isLoading={isLoadingRisks}
        onRefresh={refreshRisks}
        csvFileName="risks"
        leftSlot={showActions ? actions() : undefined}
        rightSlot={<ControlItemsMenu items={controlItems} />}
      />
      {openCreateRiskForm && (
        <CreateRiskForm
          createForTeamId={teamId}
          onFormCreate={() => onCreateRiskFormClosed(true)}
          onFormCancel={() => onCreateRiskFormClosed(false)}
        />
      )}
      {openUpdateRiskForm && editRiskKey !== undefined && (
        <EditRiskForm
          riskKey={editRiskKey}
          onFormSave={() => onEditRiskFormClosed(true)}
          onFormCancel={() => onEditRiskFormClosed(false)}
        />
      )}
    </>
  )
}

export default RisksGrid
