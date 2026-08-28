'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ProjectLifecycleDetailsDto,
  ProjectLifecycleStageDto,
} from '@/src/services/wayd-api'
import {
  useRemoveProjectLifecycleStageMutation,
  useReorderProjectLifecycleStagesMutation,
} from '@/src/store/features/ppm/project-lifecycles-api'
import { App, Button } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { useMemo, useState } from 'react'
import AddProjectLifecycleStageForm from './add-project-lifecycle-stage-form'
import EditProjectLifecycleStageForm from './edit-project-lifecycle-stage-form'
import { isApiError, type ApiError } from '@/src/utils'

export interface ProjectLifecycleStagesListProps {
  lifecycle: ProjectLifecycleDetailsDto
  canManageStages: boolean
  loadData?: () => void
}

interface RowMenuProps {
  stage: ProjectLifecycleStageDto
  sortedStages: ProjectLifecycleStageDto[]
  onEditClicked: (stage: ProjectLifecycleStageDto) => void
  onDeleteClicked: (stage: ProjectLifecycleStageDto) => void
  onMoveClicked: (stage: ProjectLifecycleStageDto, direction: 'up' | 'down') => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.stage) return []

  const index = props.sortedStages.findIndex((p) => p.id === props.stage.id)
  const isFirst = index === 0
  const isLast = index === props.sortedStages.length - 1

  const items: ItemType[] = [
    {
      key: 'edit',
      label: 'Edit',
      onClick: () => props.onEditClicked(props.stage),
    },
  ]

  if (!isFirst) {
    items.push({
      key: 'move-up',
      label: 'Move Up',
      onClick: () => props.onMoveClicked(props.stage, 'up'),
    })
  }

  if (!isLast) {
    items.push({
      key: 'move-down',
      label: 'Move Down',
      onClick: () => props.onMoveClicked(props.stage, 'down'),
    })
  }

  items.push(
    { key: 'divider', type: 'divider' },
    {
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.stage),
    },
  )

  return items
}

const ProjectLifecycleStagesList = ({
  lifecycle,
  canManageStages,
  loadData,
}: ProjectLifecycleStagesListProps) => {
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const [openAddStageForm, setOpenAddStageForm] = useState(false)
  const [editingStage, setEditingStage] =
    useState<ProjectLifecycleStageDto | null>(null)

  const [removeProjectLifecycleStage] =
    useRemoveProjectLifecycleStageMutation()
  const [reorderStages] = useReorderProjectLifecycleStagesMutation()

  const sortedStages = useMemo(
    () =>
      !lifecycle?.stages
        ? []
        : [...lifecycle.stages].sort((a, b) => a.order - b.order),
    [lifecycle?.stages],
  )

  const columns = useMemo<ColumnDef<ProjectLifecycleStageDto, any>[]>(() => {
    const handleEdit = (stage: ProjectLifecycleStageDto) => {
      setEditingStage(stage)
    }

    const handleDeleteStage = (stage: ProjectLifecycleStageDto) => {
      modal.confirm({
        title: 'Are you sure you want to delete this stage?',
        content: `${stage.order} - ${stage.name}`,
        okText: 'Delete',
        okType: 'danger',
        onOk: async () => {
          try {
            const response = await removeProjectLifecycleStage({
              lifecycleId: lifecycle.id,
              stageId: stage.id,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Stage deleted successfully.')
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            messageApi.error(
              apiError.detail ??
                'An unexpected error occurred while deleting the stage.',
            )
            console.log(error)
          }
        },
      })
    }

    const handleMove = async (stage: ProjectLifecycleStageDto, direction: 'up' | 'down') => {
      const ordered = [...sortedStages]
      const index = ordered.findIndex((p) => p.id === stage.id)
      if (index < 0) return

      const swapIndex = direction === 'up' ? index - 1 : index + 1
      if (swapIndex < 0 || swapIndex >= ordered.length) return

      ;[ordered[index], ordered[swapIndex]] = [ordered[swapIndex], ordered[index]]

      try {
        const response = await reorderStages({
          lifecycleId: lifecycle.id,
          orderedStageIds: ordered.map((p) => p.id),
        })
        if (response.error) {
          throw response.error
        }
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ?? 'An error occurred while reordering stages.',
        )
        console.error(error)
      }
    }

    return [
      createActionsColumn<ProjectLifecycleStageDto>({
        hide: !canManageStages,
        ariaLabel: 'Stage actions',
        getItems: (stage) =>
          getRowMenuItems({
            stage,
            sortedStages,
            onEditClicked: handleEdit,
            onDeleteClicked: handleDeleteStage,
            onMoveClicked: handleMove,
          }),
      }),
      {
        id: 'order',
        accessorKey: 'order',
        header: 'Order',
        size: 90,
        enableColumnFilter: false,
      },
      { id: 'name', accessorKey: 'name', header: 'Name', size: 200 },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 400,
      },
    ]}, [canManageStages, sortedStages, modal, removeProjectLifecycleStage, reorderStages, lifecycle.id, messageApi])

  const actions = canManageStages ? (
    <Button type="primary" size="small" onClick={() => setOpenAddStageForm(true)}>
      Add Stage
    </Button>
  ) : null

  return (
    <>
      {/* No fixed height: as the record's only section this fills the area
          below the identity bar, rather than sitting in a 300px pane with
          empty space beneath it. */}
      <WaydGrid
        columns={columns}
        data={sortedStages}
        leftSlot={actions}
        onRefresh={loadData}
        persistStateKey="settings-project-lifecycle-stages"
        csvFileName="project-lifecycle-stages"
      />
      {openAddStageForm && (
        <AddProjectLifecycleStageForm
          lifecycleId={lifecycle.id}
          onFormComplete={() => setOpenAddStageForm(false)}
          onFormCancel={() => setOpenAddStageForm(false)}
        />
      )}
      {editingStage && (
        <EditProjectLifecycleStageForm
          lifecycleId={lifecycle.id}
          stage={editingStage}
          onFormComplete={() => setEditingStage(null)}
          onFormCancel={() => setEditingStage(null)}
        />
      )}
    </>
  )
}

export default ProjectLifecycleStagesList
