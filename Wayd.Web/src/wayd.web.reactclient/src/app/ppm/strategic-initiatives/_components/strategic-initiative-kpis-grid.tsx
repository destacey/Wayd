'use client'

import { WaydTooltip } from '@/src/components/common'
import { useMessage } from '@/src/components/contexts/messaging'
import { WaydStatisticNumber } from '@/src/components/common/metrics'
import {
  WaydGrid,
  useGridDragHandle,
  type GridColumnContext,
  type RowReorderEvent,
} from '@/src/components/common/wayd-grid'
import { StrategicInitiativeKpiListDto } from '@/src/services/wayd-api'
import { useReorderStrategicInitiativeKpisMutation } from '@/src/store/features/ppm/strategic-initiatives-api'
import { isApiError } from '@/src/utils'
import { HolderOutlined, MoreOutlined } from '@ant-design/icons'
import type { ColumnDef } from '@tanstack/react-table'
import { Button, Dropdown, Flex, MenuProps, theme } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { FC, useState, useMemo } from 'react'
import {
  AddStrategicInitiativeKpiMeasurementForm,
  DeleteStrategicInitiativeKpiForm,
  EditStrategicInitiativeKpiForm,
  ManageStrategicInitiativeKpiCheckpointPlanForm,
  StrategicInitiativeKpiDetailsDrawer,
} from '.'

export interface StrategicInitiativeKpisGridProps {
  strategicInitiativeId: string
  kpis: StrategicInitiativeKpiListDto[]
  canManageKpis: boolean
  isLoading: boolean
  refetch: () => void
  gridHeight?: number | undefined
  isReadOnly?: boolean
  viewSelector?: React.ReactNode
}

interface RowMenuProps extends MenuProps {
  kpiId: string
  strategicInitiativeId: string
  canManageKpis: boolean
  onEditKpiMenuClicked: (id: string) => void
  onDeleteKpiMenuClicked: (id: string) => void
  onAddMeasurementMenuClicked: (id: string) => void
  onManageCheckpointPlanMenuClicked: (id: string) => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (
    !props.kpiId ||
    !props.canManageKpis ||
    !props.onEditKpiMenuClicked ||
    !props.onDeleteKpiMenuClicked ||
    !props.onAddMeasurementMenuClicked ||
    !props.onManageCheckpointPlanMenuClicked
  ) {
    return []
  }

  return [
    {
      key: 'edit-kpi',
      label: 'Edit KPI',
      onClick: () => props.onEditKpiMenuClicked(props.kpiId),
    },
    {
      key: 'delete-kpi',
      label: 'Delete KPI',
      danger: true,
      onClick: () => props.onDeleteKpiMenuClicked(props.kpiId),
    },
    { key: 'divider', type: 'divider' },
    {
      key: 'manage-checkpoint-plan',
      label: 'Manage Checkpoint Plan',
      onClick: () => props.onManageCheckpointPlanMenuClicked(props.kpiId),
    },
    {
      key: 'add-measurement',
      label: 'Add Measurement',
      onClick: () => props.onAddMeasurementMenuClicked(props.kpiId),
    },
  ]
}

// Disabled (with tooltip) while sort/filter/search make the order ambiguous.
const DragHandleCell: FC<{ isDragEnabled: boolean }> = ({ isDragEnabled }) => {
  const { token } = theme.useToken()
  const { listeners, attributes } = useGridDragHandle()

  return (
    <WaydTooltip
      title={
        isDragEnabled
          ? undefined
          : 'Clear sorting, filters, and search to reorder KPIs.'
      }
    >
      <span
        {...(isDragEnabled ? { ...listeners, ...attributes } : {})}
        style={{
          cursor: isDragEnabled ? 'grab' : 'not-allowed',
          color: isDragEnabled
            ? token.colorTextTertiary
            : token.colorTextDisabled,
          display: 'inline-flex',
          padding: '0 4px',
          touchAction: 'none',
        }}
        aria-label="Drag to reorder"
        aria-disabled={!isDragEnabled}
      >
        <HolderOutlined />
      </span>
    </WaydTooltip>
  )
}

const StrategicInitiativeKpisGrid: FC<StrategicInitiativeKpisGridProps> = (
  props,
) => {
  const {
    strategicInitiativeId,
    kpis,
    canManageKpis,
    isLoading,
    refetch,
    gridHeight,
    isReadOnly,
    viewSelector,
  } = props

  const [selectedKpiId, setSelectedKpiId] = useState<string | null>(null)
  const [openKpiDetailsDrawer, setOpenKpiDetailsDrawer] =
    useState<boolean>(false)
  const [openEditKpiForm, setOpenEditKpiForm] = useState<boolean>(false)
  const [openDeleteKpiForm, setOpenDeleteKpiForm] = useState<boolean>(false)
  const [openAddMeasurementForm, setOpenAddMeasurementForm] =
    useState<boolean>(false)
  const [openManageCheckpointPlanForm, setOpenManageCheckpointPlanForm] =
    useState<boolean>(false)

  const messageApi = useMessage()
  const [reorderKpis] = useReorderStrategicInitiativeKpisMutation()

  const canReorder = canManageKpis && !isReadOnly

  const onEditKpiFormClosed = (wasSaved: boolean) => {
    setOpenEditKpiForm(false)
    setSelectedKpiId(null)
    if (wasSaved) {
      refresh()
    }
  }

  const onDeleteKpiFormClosed = (wasSaved: boolean) => {
    setOpenDeleteKpiForm(false)
    setSelectedKpiId(null)
    if (wasSaved) {
      refresh()
    }
  }

  const onAddMeasurementFormClosed = (wasSaved: boolean) => {
    setOpenAddMeasurementForm(false)
    setSelectedKpiId(null)
    if (wasSaved) {
      refresh()
    }
  }

  const onManageCheckpointPlanFormClosed = (wasSaved: boolean) => {
    setOpenManageCheckpointPlanForm(false)
    setSelectedKpiId(null)
    if (wasSaved) {
      refresh()
    }
  }

  const columns = useMemo(
    () => {
      const onViewDetailsMenuClicked = (id: string) => {
        setSelectedKpiId(id)
        setOpenKpiDetailsDrawer(true)
      }

      const onEditKpiMenuClicked = (id: string) => {
        setSelectedKpiId(id)
        setOpenEditKpiForm(true)
      }

      const onDeleteKpiMenuClicked = (id: string) => {
        setSelectedKpiId(id)
        setOpenDeleteKpiForm(true)
      }

      const onAddMeasurementMenuClicked = (id: string) => {
        setSelectedKpiId(id)
        setOpenAddMeasurementForm(true)
      }

      const onManageCheckpointPlanMenuClicked = (id: string) => {
        setSelectedKpiId(id)
        setOpenManageCheckpointPlanForm(true)
      }

      return (
        context: GridColumnContext,
      ): ColumnDef<StrategicInitiativeKpiListDto, any>[] => [
        {
          id: 'actions',
          header: '',
          size: 70,
          enableSorting: false,
          enableColumnFilter: false,
          enableResizing: false,
          enableGlobalFilter: false,
          meta: { hide: !canManageKpis || isReadOnly },
          cell: ({ row }) => {
            const menuItems = getRowMenuItems({
              kpiId: row.original.id,
              strategicInitiativeId: strategicInitiativeId,
              canManageKpis,
              onEditKpiMenuClicked,
              onDeleteKpiMenuClicked,
              onAddMeasurementMenuClicked,
              onManageCheckpointPlanMenuClicked,
            })

            return (
              <Flex align="center" gap={2}>
                {canReorder && (
                  <DragHandleCell isDragEnabled={context.isDragEnabled} />
                )}
                {menuItems.length > 0 && (
                  <Dropdown menu={{ items: menuItems }} trigger={['click']}>
                    <Button type="text" size="small" icon={<MoreOutlined />} />
                  </Dropdown>
                )}
              </Flex>
            )
          },
        },
        { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
        {
          id: 'name',
          accessorKey: 'name',
          header: 'Name',
          size: 300,
          cell: ({ row }) => (
            <Button
              type="link"
              size="small"
              style={{ padding: 0 }}
              onClick={() => onViewDetailsMenuClicked(row.original.id)}
            >
              {row.original.name}
            </Button>
          ),
        },
        {
          id: 'targetValue',
          accessorKey: 'targetValue',
          header: 'Target Value',
          size: 125,
          cell: ({ getValue }) => (
            <WaydStatisticNumber value={getValue<number>()} />
          ),
        },
        {
          id: 'actualValue',
          accessorKey: 'actualValue',
          header: 'Actual Value',
          size: 125,
          cell: ({ getValue }) => (
            <WaydStatisticNumber value={getValue<number>()} />
          ),
        },
        {
          id: 'format',
          accessorFn: (row) =>
            [row.prefix, row.suffix].filter(Boolean).join(' / ') || '-',
          header: 'Format',
          size: 125,
        },
        {
          id: 'targetDirection',
          accessorKey: 'targetDirection',
          header: 'Target Direction',
          size: 125,
          meta: { filterType: 'set' },
        },
      ]
    },
    [canManageKpis, canReorder, isReadOnly, strategicInitiativeId],
  )

  const refresh = async () => {
    refetch()
  }

  const onRowReorder = async (
    event: RowReorderEvent<StrategicInitiativeKpiListDto>,
  ) => {
    try {
      await reorderKpis({
        strategicInitiativeId,
        request: { orderedKpiIds: event.orderedData.map((kpi) => kpi.id) },
      }).unwrap()
    } catch (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'Failed to reorder KPIs. Please try again.',
      )
      refetch()
    }
  }

  return (
    <>
      <WaydGrid
        columns={columns}
        data={kpis}
        onRefresh={refresh}
        isLoading={isLoading}
        height={gridHeight}
        emptyMessage="No KPIs found."
        getRowId={(kpi) => kpi.id}
        rightSlot={viewSelector}
        onRowReorder={canReorder ? onRowReorder : undefined}
      />
      {selectedKpiId && (
        <StrategicInitiativeKpiDetailsDrawer
          strategicInitiativeId={strategicInitiativeId}
          kpiId={selectedKpiId}
          drawerOpen={openKpiDetailsDrawer}
          onDrawerClose={() => {
            setOpenKpiDetailsDrawer(false)
            setSelectedKpiId(null)
          }}
          canManageKpis={canManageKpis && !isReadOnly}
          onRefresh={refresh}
        />
      )}
      {openEditKpiForm && selectedKpiId && (
        <EditStrategicInitiativeKpiForm
          strategicInitiativeId={strategicInitiativeId}
          kpiId={selectedKpiId}
          onFormComplete={() => onEditKpiFormClosed(true)}
          onFormCancel={() => onEditKpiFormClosed(false)}
        />
      )}
      {openDeleteKpiForm && selectedKpiId && (
        <DeleteStrategicInitiativeKpiForm
          strategicInitiativeId={strategicInitiativeId}
          kpi={kpis.find((kpi) => kpi.id === selectedKpiId)!}
          onFormComplete={() => onDeleteKpiFormClosed(true)}
          onFormCancel={() => onDeleteKpiFormClosed(false)}
        />
      )}
      {openAddMeasurementForm && selectedKpiId && (
        <AddStrategicInitiativeKpiMeasurementForm
          strategicInitiativeId={strategicInitiativeId}
          kpiId={selectedKpiId}
          onFormComplete={() => onAddMeasurementFormClosed(true)}
          onFormCancel={() => onAddMeasurementFormClosed(false)}
        />
      )}
      {openManageCheckpointPlanForm && selectedKpiId && (
        <ManageStrategicInitiativeKpiCheckpointPlanForm
          strategicInitiativeId={strategicInitiativeId}
          kpiId={selectedKpiId}
          onFormComplete={() => onManageCheckpointPlanFormClosed(true)}
          onFormCancel={() => onManageCheckpointPlanFormClosed(false)}
        />
      )}
    </>
  )
}

export default StrategicInitiativeKpisGrid
