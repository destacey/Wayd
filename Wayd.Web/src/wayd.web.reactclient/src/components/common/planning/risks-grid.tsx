import Link from 'next/link'
import { useMemo, useState } from 'react'
import WaydGrid from '../wayd-grid'
import { RiskListDto } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { Button } from 'antd'
import useAuth from '../../contexts/auth'
import CreateRiskForm from './create-risk-form'
import { EditOutlined } from '@ant-design/icons'
import EditRiskForm from './edit-risk-form'
import { NestedTeamNameLinkCellRenderer } from '../wayd-grid-cell-renderers'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import { ControlItemSwitch } from '../control-items-menu'

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

const RiskLinkCellRenderer = ({ value, data }: ICellRendererParams<RiskListDto>) => {
  return <Link href={`/planning/risks/${data!.key}`}>{value}</Link>
}

const AssigneeLinkCellRenderer = ({ value, data }: ICellRendererParams<RiskListDto>) => {
  return (
    <Link href={`/organizations/employees/${data!.assignee?.key}`}>{value}</Link>
  )
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

  // TODO: dates are formatted correctly and filter, but the filter is string based, not date based
  const columnDefs = useMemo<ColDef<RiskListDto>[]>(() => {
    const editRiskButtonClicked = (key: number) => {
      setEditRiskKey(key)
      setOpenUpdateRiskForm(true)
    }

    return [
    {
      width: 50,
      filter: false,
      sortable: false,
      resizable: false,
      hide: !canUpdateRisks,
      cellRenderer: (params: ICellRendererParams<RiskListDto>) => {
        if (!params.data) return null
        return (
          canUpdateRisks && (
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              onClick={() => editRiskButtonClicked(params.data!.key)}
            />
          )
        )
      },
    },
    { field: 'id', hide: true },
    { field: 'key', width: 90 },
    { field: 'summary', width: 300, cellRenderer: RiskLinkCellRenderer },
    {
      field: 'team.name',
      headerName: 'Team',
      cellRenderer: NestedTeamNameLinkCellRenderer,
      hide: hideTeam,
    },
    { field: 'status', width: 125, hide: includeClosed === false },
    { field: 'category', width: 125 },
    { field: 'exposure', width: 125 },
    {
      field: 'followUpDate',
      type: 'dateOnly',
    },
    {
      field: 'assignee.name',
      headerName: 'Assignee',
      cellRenderer: AssigneeLinkCellRenderer,
    },
    {
      field: 'reportedOn',
      type: 'dateOnly',
    },
  ]}, [canUpdateRisks, hideTeam, includeClosed])

  return (
    <>
      {/* TODO:  setup dynamic height */}
      <WaydGrid
        height={gridHeight}
        columnDefs={columnDefs}
        rowData={risks}
        loading={isLoadingRisks}
        loadData={refreshRisks}
        actions={showActions && actions()}
        gridControlMenuItems={controlItems}
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
