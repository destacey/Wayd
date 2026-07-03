'use client'

import {
  WaydGrid2,
  createActionsColumn,
} from '@/src/components/common/wayd-grid2'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ProjectLifecycleDetailsDto,
  ProjectLifecyclePhaseDto,
} from '@/src/services/wayd-api'
import {
  useRemoveProjectLifecyclePhaseMutation,
  useReorderProjectLifecyclePhasesMutation,
} from '@/src/store/features/ppm/project-lifecycles-api'
import { App, Button } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import type { ColumnDef } from '@tanstack/react-table'
import { useMemo, useState } from 'react'
import AddProjectLifecyclePhaseForm from './add-project-lifecycle-phase-form'
import EditProjectLifecyclePhaseForm from './edit-project-lifecycle-phase-form'
import { isApiError, type ApiError } from '@/src/utils'

export interface ProjectLifecyclePhasesListProps {
  lifecycle: ProjectLifecycleDetailsDto
  canManagePhases: boolean
  loadData?: () => void
}

interface RowMenuProps {
  phase: ProjectLifecyclePhaseDto
  sortedPhases: ProjectLifecyclePhaseDto[]
  onEditClicked: (phase: ProjectLifecyclePhaseDto) => void
  onDeleteClicked: (phase: ProjectLifecyclePhaseDto) => void
  onMoveClicked: (phase: ProjectLifecyclePhaseDto, direction: 'up' | 'down') => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.phase) return []

  const index = props.sortedPhases.findIndex((p) => p.id === props.phase.id)
  const isFirst = index === 0
  const isLast = index === props.sortedPhases.length - 1

  const items: ItemType[] = [
    {
      key: 'edit',
      label: 'Edit',
      onClick: () => props.onEditClicked(props.phase),
    },
  ]

  if (!isFirst) {
    items.push({
      key: 'move-up',
      label: 'Move Up',
      onClick: () => props.onMoveClicked(props.phase, 'up'),
    })
  }

  if (!isLast) {
    items.push({
      key: 'move-down',
      label: 'Move Down',
      onClick: () => props.onMoveClicked(props.phase, 'down'),
    })
  }

  items.push(
    { key: 'divider', type: 'divider' },
    {
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.phase),
    },
  )

  return items
}

const ProjectLifecyclePhasesList = ({
  lifecycle,
  canManagePhases,
  loadData,
}: ProjectLifecyclePhasesListProps) => {
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const [openAddPhaseForm, setOpenAddPhaseForm] = useState(false)
  const [editingPhase, setEditingPhase] =
    useState<ProjectLifecyclePhaseDto | null>(null)

  const [removeProjectLifecyclePhase] =
    useRemoveProjectLifecyclePhaseMutation()
  const [reorderPhases] = useReorderProjectLifecyclePhasesMutation()

  const sortedPhases = useMemo(
    () =>
      !lifecycle?.phases
        ? []
        : [...lifecycle.phases].sort((a, b) => a.order - b.order),
    [lifecycle?.phases],
  )

  const columns = useMemo<ColumnDef<ProjectLifecyclePhaseDto, any>[]>(() => {
    const handleEdit = (phase: ProjectLifecyclePhaseDto) => {
      setEditingPhase(phase)
    }

    const handleDeletePhase = (phase: ProjectLifecyclePhaseDto) => {
      modal.confirm({
        title: 'Are you sure you want to delete this phase?',
        content: `${phase.order} - ${phase.name}`,
        okText: 'Delete',
        okType: 'danger',
        onOk: async () => {
          try {
            const response = await removeProjectLifecyclePhase({
              lifecycleId: lifecycle.id,
              phaseId: phase.id,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Phase deleted successfully.')
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            messageApi.error(
              apiError.detail ??
                'An unexpected error occurred while deleting the phase.',
            )
            console.log(error)
          }
        },
      })
    }

    const handleMove = async (phase: ProjectLifecyclePhaseDto, direction: 'up' | 'down') => {
      const ordered = [...sortedPhases]
      const index = ordered.findIndex((p) => p.id === phase.id)
      if (index < 0) return

      const swapIndex = direction === 'up' ? index - 1 : index + 1
      if (swapIndex < 0 || swapIndex >= ordered.length) return

      ;[ordered[index], ordered[swapIndex]] = [ordered[swapIndex], ordered[index]]

      try {
        const response = await reorderPhases({
          lifecycleId: lifecycle.id,
          orderedPhaseIds: ordered.map((p) => p.id),
        })
        if (response.error) {
          throw response.error
        }
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ?? 'An error occurred while reordering phases.',
        )
        console.error(error)
      }
    }

    return [
      createActionsColumn<ProjectLifecyclePhaseDto>({
        hide: !canManagePhases,
        ariaLabel: 'Phase actions',
        getItems: (phase) =>
          getRowMenuItems({
            phase,
            sortedPhases,
            onEditClicked: handleEdit,
            onDeleteClicked: handleDeletePhase,
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
    ]}, [canManagePhases, sortedPhases, modal, removeProjectLifecyclePhase, reorderPhases, lifecycle.id, messageApi])

  const actions = canManagePhases ? (
    <Button type="primary" size="small" onClick={() => setOpenAddPhaseForm(true)}>
      Add Phase
    </Button>
  ) : null

  return (
    <>
      <WaydGrid2
        height={300}
        columns={columns}
        data={sortedPhases}
        leftSlot={actions}
        onRefresh={loadData}
        csvFileName="project-lifecycle-phases"
      />
      {openAddPhaseForm && (
        <AddProjectLifecyclePhaseForm
          lifecycleId={lifecycle.id}
          onFormComplete={() => setOpenAddPhaseForm(false)}
          onFormCancel={() => setOpenAddPhaseForm(false)}
        />
      )}
      {editingPhase && (
        <EditProjectLifecyclePhaseForm
          lifecycleId={lifecycle.id}
          phase={editingPhase}
          onFormComplete={() => setEditingPhase(null)}
          onFormCancel={() => setEditingPhase(null)}
        />
      )}
    </>
  )
}

export default ProjectLifecyclePhasesList
