'use client'

import { useState } from 'react'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import {
  useDeleteTeamOfTeamsOperatingModelMutation,
  useGetTeamOfTeamsOperatingModelsQuery,
} from '@/src/store/features/organizations/team-api'
import { TeamOfTeamsOperatingModelDetailsDto } from '@/src/services/wayd-api'
import { Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import EditTeamOfTeamsOperatingModelForm from './edit-team-of-teams-operating-model-form'
import DeleteOperatingModelForm, {
  findReinstatedModel,
} from '@/src/app/organizations/_components/delete-operating-model-form'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'

interface TeamOfTeamsOperatingModelsGridProps {
  teamId: string
  canUpdate: boolean
}

const TeamOfTeamsOperatingModelsGrid = ({
  teamId,
  canUpdate,
}: TeamOfTeamsOperatingModelsGridProps) => {
  const [selectedModelId, setSelectedModelId] = useState<string | null>(null)
  const [modelToDelete, setModelToDelete] =
    useState<TeamOfTeamsOperatingModelDetailsDto | null>(null)

  const {
    data: operatingModelsData,
    isLoading,
    refetch,
  } = useGetTeamOfTeamsOperatingModelsQuery(teamId)
  const [deleteOperatingModel] = useDeleteTeamOfTeamsOperatingModelMutation()

  const totalModelsCount = operatingModelsData?.length ?? 0

  // Only the current model can be edited here, and deleted only while an
  // earlier one remains to reinstate.
  const getRowMenuItems = (
    model: TeamOfTeamsOperatingModelDetailsDto,
  ): ItemType[] => {
    if (!model || !canUpdate || !model.isCurrent) return []

    const items: ItemType[] = [
      {
        key: 'edit',
        label: 'Edit',
        onClick: () => setSelectedModelId(model.id),
      },
    ]
    if (totalModelsCount > 1) {
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => setModelToDelete(model),
      })
    }
    return items
  }

  const columns: ColumnDef<TeamOfTeamsOperatingModelDetailsDto, any>[] = [
    createActionsColumn<TeamOfTeamsOperatingModelDetailsDto>({
      unavailable: !canUpdate,
      ariaLabel: 'Operating model actions',
      getItems: getRowMenuItems,
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
      id: 'timeZone',
      accessorKey: 'timeZone',
      header: 'Time Zone',
      meta: { filterType: 'set' },
    },
    {
      id: 'isCurrent',
      accessorFn: (row) => (row.isCurrent ? 'Current' : 'Historical'),
      header: 'Status',
      meta: { filterType: 'set' },
      cell: ({ row }) =>
        row.original.isCurrent ? (
          <Tag color="green">Current</Tag>
        ) : (
          <Tag>Historical</Tag>
        ),
    },
  ]

  return (
    <>
      <WaydGrid
        columns={columns}
        data={operatingModelsData ?? []}
        isLoading={isLoading}
        onRefresh={refetch}
        persistStateKey="team-of-teams-operating-models"
        csvFileName="team-of-teams-operating-models"
      />

      {selectedModelId && (
        <EditTeamOfTeamsOperatingModelForm
          teamId={teamId}
          operatingModelId={selectedModelId}
          onFormComplete={() => setSelectedModelId(null)}
          onFormCancel={() => setSelectedModelId(null)}
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

export default TeamOfTeamsOperatingModelsGrid
