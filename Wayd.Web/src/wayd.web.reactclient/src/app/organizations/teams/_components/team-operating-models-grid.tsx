'use client'

import { useState, useMemo } from 'react'
import WaydGrid from '@/src/components/common/wayd-grid'
import {
  useDeleteTeamOperatingModelMutation,
  useGetTeamOperatingModelsQuery,
} from '@/src/store/features/organizations/team-api'
import {
  SizingMethod,
  TeamOperatingModelDetailsDto,
} from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { RowMenuCellRenderer } from '@/src/components/common/wayd-grid-cell-renderers'
import { Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import EditTeamOperatingModelForm from './edit-team-operating-model-form'
import { ColDef, ICellRendererParams } from 'ag-grid-community'

interface TeamOperatingModelsGridProps {
  teamId: string
  canUpdate: boolean
}

const getSizingMethodDisplayName = (sizingMethod: SizingMethod): string => {
  return sizingMethod === SizingMethod.StoryPoints
    ? 'Story Points'
    : sizingMethod
}

const StatusCellRenderer = ({
  data,
}: {
  data: TeamOperatingModelDetailsDto
}) => {
  if (!data) return null
  return data.isCurrent ? (
    <Tag color="green">Current</Tag>
  ) : (
    <Tag>Historical</Tag>
  )
}

interface RowMenuProps {
  operatingModel: TeamOperatingModelDetailsDto
  canUpdate: boolean
  totalModelsCount: number
  onEditClicked: (model: TeamOperatingModelDetailsDto) => void
  onDeleteClicked: (model: TeamOperatingModelDetailsDto) => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.operatingModel) return []

  const isCurrent = props.operatingModel.isCurrent
  // Can only edit the current operating model
  const canEdit = props.canUpdate && isCurrent
  // Can only delete the current operating model, and only if there is more than one
  const canDelete = props.canUpdate && isCurrent && props.totalModelsCount > 1

  const items: ItemType[] = []

  if (canEdit) {
    items.push({
      key: 'edit',
      label: 'Edit',
      onClick: () => props.onEditClicked(props.operatingModel),
    })
  }

  if (canDelete) {
    items.push({
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.operatingModel),
    })
  }

  return items
}

const TeamOperatingModelsGrid = ({
  teamId,
  canUpdate,
}: TeamOperatingModelsGridProps) => {
  const messageApi = useMessage()
  const [selectedModelId, setSelectedModelId] = useState<string | null>(null)
  const [showUpdateForm, setShowUpdateForm] = useState(false)

  const {
    data: operatingModelsData,
    isLoading,
    refetch,
  } = useGetTeamOperatingModelsQuery(teamId)
  const [deleteOperatingModel] = useDeleteTeamOperatingModelMutation()

  const refresh = () => {
    refetch()
  }

  const handleUpdateFormClose = () => {
    setShowUpdateForm(false)
    setSelectedModelId(null)
  }

  const totalModelsCount = operatingModelsData?.length ?? 0

  const columnDefs = useMemo<ColDef<TeamOperatingModelDetailsDto>[]>(() => {
    const handleEdit = (model: TeamOperatingModelDetailsDto) => {
      setSelectedModelId(model.id)
      setShowUpdateForm(true)
    }

    const handleDelete = async (model: TeamOperatingModelDetailsDto) => {
      try {
        await deleteOperatingModel({
          teamId,
          operatingModelId: model.id,
        }).unwrap()
        messageApi.success('Successfully deleted operating model.')
      } catch (error: any) {
        messageApi.error(
          error.detail ??
            'An unexpected error occurred while deleting the operating model.',
        )
        console.error(error)
      }
    }

    return [
      {
        width: 50,
        filter: false,
        sortable: false,
        hide: !canUpdate,
        suppressHeaderMenuButton: true,
        cellRenderer: (
          params: ICellRendererParams<TeamOperatingModelDetailsDto>,
        ) => {
          const menuItems = getRowMenuItems({
            operatingModel: params.data!,
            canUpdate,
            totalModelsCount,
            onEditClicked: handleEdit,
            onDeleteClicked: handleDelete,
          })
          if (menuItems.length === 0) return null
          return RowMenuCellRenderer({ ...params, menuItems })
        },
      },
      {
        field: 'start',
        headerName: 'Start Date',
        type: 'dateOnly',
        sort: 'desc',
      },
      {
        field: 'end',
        headerName: 'End Date',
        type: 'dateOnly',
      },
      {
        field: 'methodology',
        headerName: 'Methodology',
      },
      {
        field: 'sizingMethod',
        headerName: 'Sizing Method',
        valueGetter: (params) =>
          params.data?.sizingMethod
            ? getSizingMethodDisplayName(params.data.sizingMethod)
            : null,
      },
      {
        field: 'isCurrent',
        headerName: 'Status',
        cellRenderer: StatusCellRenderer,
      },
    ]
  }, [canUpdate, totalModelsCount, teamId, deleteOperatingModel, messageApi])

  return (
    <>
      <WaydGrid
        height={550}
        columnDefs={columnDefs}
        rowData={operatingModelsData}
        loading={isLoading}
        loadData={refresh}
      />

      {showUpdateForm && selectedModelId && (
        <EditTeamOperatingModelForm
          teamId={teamId}
          operatingModelId={selectedModelId}
          onFormComplete={() => handleUpdateFormClose()}
          onFormCancel={() => handleUpdateFormClose()}
        />
      )}
    </>
  )
}

export default TeamOperatingModelsGrid
