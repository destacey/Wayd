'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  StatusWorkflowDetailsDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import {
  useGetWorkflowOwnerTypesQuery,
  useRemoveWorkflowStatusMutation,
  useReorderWorkflowStatusesMutation,
} from '@/src/store/features/common/status-workflows-api'
import { App, Button, Space } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { useMemo, useState } from 'react'
import AddWorkflowStatusForm from './add-workflow-status-form'
import EditWorkflowStatusForm from './edit-workflow-status-form'
import { isApiError, type ApiError } from '@/src/utils'

export interface WorkflowStatusesListProps {
  statusWorkflow: StatusWorkflowDetailsDto
  loadData?: () => void
}

interface RowMenuProps {
  status: WorkflowStatusDto
  sortedStatuses: WorkflowStatusDto[]
  onEditClicked: (status: WorkflowStatusDto) => void
  onDeleteClicked: (status: WorkflowStatusDto) => void
  onMoveClicked: (status: WorkflowStatusDto, direction: 'up' | 'down') => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.status) return []

  const index = props.sortedStatuses.findIndex((s) => s.id === props.status.id)
  const isFirst = index === 0
  const isLast = index === props.sortedStatuses.length - 1

  const items: ItemType[] = [
    {
      key: 'edit',
      label: 'Edit',
      onClick: () => props.onEditClicked(props.status),
    },
  ]

  if (!isFirst) {
    items.push({
      key: 'move-up',
      label: 'Move Up',
      onClick: () => props.onMoveClicked(props.status, 'up'),
    })
  }

  if (!isLast) {
    items.push({
      key: 'move-down',
      label: 'Move Down',
      onClick: () => props.onMoveClicked(props.status, 'down'),
    })
  }

  items.push(
    { key: 'divider', type: 'divider' },
    {
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.status),
    },
  )

  return items
}

const WorkflowStatusesList = ({
  statusWorkflow,
  loadData,
}: WorkflowStatusesListProps) => {
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const [openAddForm, setOpenAddForm] = useState(false)
  const [editingStatus, setEditingStatus] = useState<WorkflowStatusDto | null>(
    null,
  )

  const [removeStatus] = useRemoveWorkflowStatusMutation()
  const [reorderStatuses] = useReorderWorkflowStatusesMutation()

  // An alias is an int whose meaning belongs to the owner type's module, so
  // the vocabulary has to come from the registry rather than a local enum.
  const { data: ownerTypes } = useGetWorkflowOwnerTypesQuery()
  const aliases = useMemo(
    () =>
      ownerTypes?.find((o) => o.key === statusWorkflow.owner?.key)?.aliases ??
      [],
    [ownerTypes, statusWorkflow.owner?.key],
  )

  const canEdit = statusWorkflow.canEdit

  const sortedStatuses = useMemo(
    () =>
      !statusWorkflow?.statuses
        ? []
        : [...statusWorkflow.statuses].sort((a, b) => a.order - b.order),
    [statusWorkflow?.statuses],
  )

  const columns = useMemo<ColumnDef<WorkflowStatusDto, any>[]>(() => {
    const handleEdit = (status: WorkflowStatusDto) => {
      setEditingStatus(status)
    }

    const handleDelete = (status: WorkflowStatusDto) => {
      modal.confirm({
        title: 'Are you sure you want to delete this status?',
        content: status.name,
        okText: 'Delete',
        okType: 'danger',
        onOk: async () => {
          try {
            const response = await removeStatus({
              workflowId: statusWorkflow.id,
              statusId: status.id,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Status deleted successfully.')
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            messageApi.error(
              apiError.detail ??
                'An unexpected error occurred while deleting the status.',
            )
            console.error(error)
          }
        },
      })
    }

    const handleMove = async (
      status: WorkflowStatusDto,
      direction: 'up' | 'down',
    ) => {
      const ordered = [...sortedStatuses]
      const index = ordered.findIndex((s) => s.id === status.id)
      if (index < 0) return

      const swapIndex = direction === 'up' ? index - 1 : index + 1
      if (swapIndex < 0 || swapIndex >= ordered.length) return
      ;[ordered[index], ordered[swapIndex]] = [
        ordered[swapIndex],
        ordered[index],
      ]

      try {
        // The whole list goes over, not just the pair that moved — the API
        // refuses a partial list rather than guessing what the rest should be.
        const response = await reorderStatuses({
          workflowId: statusWorkflow.id,
          request: { orderedStatusIds: ordered.map((s) => s.id) },
        })
        if (response.error) {
          throw response.error
        }
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ?? 'An error occurred while reordering statuses.',
        )
        console.error(error)
      }
    }

    return [
      createActionsColumn<WorkflowStatusDto>({
        hide: !canEdit,
        ariaLabel: 'Status actions',
        getItems: (status) =>
          getRowMenuItems({
            status,
            sortedStatuses,
            onEditClicked: handleEdit,
            onDeleteClicked: handleDelete,
            onMoveClicked: handleMove,
          }),
      }),
      {
        id: 'order',
        accessorKey: 'order',
        header: 'Order',
        size: 80,
        enableColumnFilter: false,
      },
      { id: 'name', accessorKey: 'name', header: 'Name', size: 220 },
      {
        id: 'category',
        accessorFn: (row) => row.category?.name,
        header: 'Category',
        size: 130,
        meta: { filterType: 'set' },
      },
      {
        id: 'alias',
        accessorFn: (row) => row.aliasName,
        header: 'Meaning',
        size: 150,
        meta: { filterType: 'set' },
      },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 300,
      },
    ]
  }, [
    canEdit,
    sortedStatuses,
    modal,
    removeStatus,
    reorderStatuses,
    statusWorkflow.id,
    messageApi,
  ])

  const actions = (
    <Space>
      {canEdit && (
        <Button type="primary" size="small" onClick={() => setOpenAddForm(true)}>
          Add Status
        </Button>
      )}
    </Space>
  )

  return (
    <>
      {/* No fixed height: as the record's only section this fills the area
          below the identity bar, rather than sitting in a 400px pane with
          empty space beneath it. */}
      <WaydGrid
        columns={columns}
        data={sortedStatuses}
        leftSlot={actions}
        onRefresh={loadData}
        persistStateKey="settings-status-workflow-statuses"
        csvFileName="workflow-statuses"
      />
      {openAddForm && (
        <AddWorkflowStatusForm
          workflowId={statusWorkflow.id}
          statuses={sortedStatuses}
          aliases={aliases}
          onFormComplete={() => setOpenAddForm(false)}
          onFormCancel={() => setOpenAddForm(false)}
        />
      )}
      {editingStatus && (
        <EditWorkflowStatusForm
          workflowId={statusWorkflow.id}
          status={editingStatus}
          statuses={sortedStatuses}
          aliases={aliases}
          onFormComplete={() => setEditingStatus(null)}
          onFormCancel={() => setEditingStatus(null)}
        />
      )}
    </>
  )
}

export default WorkflowStatusesList
