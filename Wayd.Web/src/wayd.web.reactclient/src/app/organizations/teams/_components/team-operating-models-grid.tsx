'use client'

import { useState, useMemo } from 'react'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import {
  useDeleteTeamOperatingModelMutation,
  useGetTeamOperatingModelsQuery,
} from '@/src/store/features/organizations/team-api'
import { TeamOperatingModelDetailsDto } from '@/src/services/wayd-api'
import { sizingMethodLabel } from '@/src/utils'
import { formatWorkingDays } from '@/src/components/common/scheduling'
import { Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import EditTeamOperatingModelForm from './edit-team-operating-model-form'
import DeleteOperatingModelForm, {
  findReinstatedModel,
} from '@/src/app/organizations/_components/delete-operating-model-form'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
interface TeamOperatingModelsGridProps {
  teamId: string
  canUpdate: boolean
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
  const [selectedModelId, setSelectedModelId] = useState<string | null>(null)
  const [showUpdateForm, setShowUpdateForm] = useState(false)
  const [modelToDelete, setModelToDelete] =
    useState<TeamOperatingModelDetailsDto | null>(null)

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

  const columns = useMemo<
    ColumnDef<TeamOperatingModelDetailsDto, any>[]
  >(() => {
    const handleEdit = (model: TeamOperatingModelDetailsDto) => {
      setSelectedModelId(model.id)
      setShowUpdateForm(true)
    }

    const handleDelete = (model: TeamOperatingModelDetailsDto) => {
      setModelToDelete(model)
    }

    return [
      createActionsColumn<TeamOperatingModelDetailsDto>({
        unavailable: !canUpdate,
        ariaLabel: 'Operating model actions',
        getItems: (operatingModel) =>
          getRowMenuItems({
            operatingModel,
            canUpdate,
            totalModelsCount,
            onEditClicked: handleEdit,
            onDeleteClicked: handleDelete,
          }),
      }),
      {
        id: 'start',
        accessorKey: 'start',
        header: 'Start Date',
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'end',
        accessorKey: 'end',
        header: 'End Date',
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'methodology',
        accessorKey: 'methodology',
        header: 'Methodology',
        meta: { filterType: 'set' },
      },
      {
        id: 'sizingMethod',
        accessorFn: (row) =>
          row.sizingMethod ? sizingMethodLabel(row.sizingMethod) : null,
        header: 'Sizing Method',
        meta: { filterType: 'set' },
      },
      {
        id: 'timeZone',
        accessorKey: 'timeZone',
        header: 'Time Zone',
        meta: { filterType: 'set' },
      },
      {
        id: 'commitmentGraceDays',
        accessorKey: 'commitmentGraceDays',
        header: 'Grace Days',
      },
      {
        id: 'workingDays',
        accessorFn: (row) => formatWorkingDays(row.workingDays),
        header: 'Working Days',
        meta: { filterType: 'set' },
      },
      {
        id: 'isCurrent',
        accessorFn: (row) => (row.isCurrent ? 'Current' : 'Historical'),
        header: 'Status',
        meta: { filterType: 'set' },
        cell: ({ row }) => <StatusCellRenderer data={row.original} />,
      },
    ]
  }, [canUpdate, totalModelsCount])

  return (
    <>
      <WaydGrid
        columns={columns}
        data={operatingModelsData ?? []}
        isLoading={isLoading}
        onRefresh={refresh}
        persistStateKey="team-operating-models"
        csvFileName="team-operating-models"
      />

      {showUpdateForm && selectedModelId && (
        <EditTeamOperatingModelForm
          teamId={teamId}
          operatingModelId={selectedModelId}
          onFormComplete={() => handleUpdateFormClose()}
          onFormCancel={() => handleUpdateFormClose()}
        />
      )}

      {modelToDelete && (
        <DeleteOperatingModelForm
          operatingModel={modelToDelete}
          reinstatedModel={findReinstatedModel(operatingModelsData)}
          deleteOperatingModel={() =>
            deleteOperatingModel({
              teamId,
              operatingModelId: modelToDelete.id,
            }).unwrap()
          }
          onFormComplete={() => setModelToDelete(null)}
          onFormCancel={() => setModelToDelete(null)}
        />
      )}
    </>
  )
}

export default TeamOperatingModelsGrid
