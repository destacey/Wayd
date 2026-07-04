'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringModelCriterionDto,
  ScoringModelDetailsDto,
} from '@/src/services/wayd-api'
import {
  useRemoveScoringModelCriterionMutation,
  useReorderScoringModelCriteriaMutation,
} from '@/src/store/features/scoring/scoring-models-api'
import { App, Button, Space, Tag, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import type { ColumnDef } from '@tanstack/react-table'
import { useMemo, useState } from 'react'
import AddScoringModelCriterionForm from './add-scoring-model-criterion-form'
import EditScoringModelCriterionForm from './edit-scoring-model-criterion-form'
import { isApiError, type ApiError } from '@/src/utils'

const { Text } = Typography

export interface ScoringModelCriteriaListProps {
  scoringModel: ScoringModelDetailsDto
  canManage: boolean
  loadData?: () => void
}

interface RowMenuProps {
  criterion: ScoringModelCriterionDto
  sortedCriteria: ScoringModelCriterionDto[]
  onEditClicked: (criterion: ScoringModelCriterionDto) => void
  onDeleteClicked: (criterion: ScoringModelCriterionDto) => void
  onMoveClicked: (
    criterion: ScoringModelCriterionDto,
    direction: 'up' | 'down',
  ) => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.criterion) return []

  const index = props.sortedCriteria.findIndex(
    (c) => c.id === props.criterion.id,
  )
  const isFirst = index === 0
  const isLast = index === props.sortedCriteria.length - 1

  const items: ItemType[] = [
    {
      key: 'edit',
      label: 'Edit',
      onClick: () => props.onEditClicked(props.criterion),
    },
  ]

  if (!isFirst) {
    items.push({
      key: 'move-up',
      label: 'Move Up',
      onClick: () => props.onMoveClicked(props.criterion, 'up'),
    })
  }

  if (!isLast) {
    items.push({
      key: 'move-down',
      label: 'Move Down',
      onClick: () => props.onMoveClicked(props.criterion, 'down'),
    })
  }

  items.push(
    { key: 'divider', type: 'divider' },
    {
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.criterion),
    },
  )

  return items
}

const ScoringModelCriteriaList = ({
  scoringModel,
  canManage,
  loadData,
}: ScoringModelCriteriaListProps) => {
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const [openAddForm, setOpenAddForm] = useState(false)
  const [editingCriterion, setEditingCriterion] =
    useState<ScoringModelCriterionDto | null>(null)

  const [removeCriterion] = useRemoveScoringModelCriterionMutation()
  const [reorderCriteria] = useReorderScoringModelCriteriaMutation()

  const sortedCriteria = useMemo(
    () =>
      !scoringModel?.criteria
        ? []
        : [...scoringModel.criteria].sort((a, b) => a.order - b.order),
    [scoringModel?.criteria],
  )

  const totalWeight = useMemo(
    () => sortedCriteria.reduce((sum, c) => sum + (c.weight ?? 0), 0),
    [sortedCriteria],
  )

  const hasAnyWeight = useMemo(
    () => sortedCriteria.some((c) => c.weight != null),
    [sortedCriteria],
  )

  const columns = useMemo<ColumnDef<ScoringModelCriterionDto, any>[]>(() => {
    const handleEdit = (criterion: ScoringModelCriterionDto) => {
      setEditingCriterion(criterion)
    }

    const handleDelete = (criterion: ScoringModelCriterionDto) => {
      modal.confirm({
        title: 'Are you sure you want to delete this criterion?',
        content: `${criterion.name} (${criterion.weight}%)`,
        okText: 'Delete',
        okType: 'danger',
        onOk: async () => {
          try {
            const response = await removeCriterion({
              scoringModelId: scoringModel.id,
              criterionId: criterion.id,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Criterion deleted successfully.')
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            messageApi.error(
              apiError.detail ??
                'An unexpected error occurred while deleting the criterion.',
            )
            console.log(error)
          }
        },
      })
    }

    const handleMove = async (
      criterion: ScoringModelCriterionDto,
      direction: 'up' | 'down',
    ) => {
      const ordered = [...sortedCriteria]
      const index = ordered.findIndex((c) => c.id === criterion.id)
      if (index < 0) return

      const swapIndex = direction === 'up' ? index - 1 : index + 1
      if (swapIndex < 0 || swapIndex >= ordered.length) return
      ;[ordered[index], ordered[swapIndex]] = [
        ordered[swapIndex],
        ordered[index],
      ]

      try {
        const response = await reorderCriteria({
          scoringModelId: scoringModel.id,
          orderedCriterionIds: ordered.map((c) => c.id),
        })
        if (response.error) {
          throw response.error
        }
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ?? 'An error occurred while reordering criteria.',
        )
        console.error(error)
      }
    }

    return [
      createActionsColumn<ScoringModelCriterionDto>({
        hide: !canManage,
        ariaLabel: 'Criterion actions',
        getItems: (criterion) =>
          getRowMenuItems({
            criterion,
            sortedCriteria,
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
      { id: 'name', accessorKey: 'name', header: 'Name', size: 250 },
      { id: 'token', accessorKey: 'token', header: 'Token', size: 110 },
      {
        id: 'scale',
        accessorFn: (row) => {
          const scaleId = row.scaleId
          if (!scaleId) return 'Free entry'
          return scoringModel.scales?.find((s) => s.id === scaleId)?.name ?? '—'
        },
        header: 'Scale',
      },
      {
        id: 'weight',
        accessorKey: 'weight',
        header: 'Weight (%)',
        size: 120,
        cell: ({ getValue }) => {
          const value = getValue() as number | null | undefined
          return value != null ? `${value}%` : ''
        },
      },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 300,
      },
    ]
  }, [
    canManage,
    sortedCriteria,
    modal,
    removeCriterion,
    reorderCriteria,
    scoringModel.id,
    scoringModel.scales,
    messageApi,
  ])

  const weightsBalanced = totalWeight === 100

  const actions = (
    <Space>
      {hasAnyWeight && (
        <Text type={weightsBalanced ? 'success' : 'secondary'}>
          Total weight: {totalWeight}%
          {!weightsBalanced && (
            <Tag color="default" style={{ marginLeft: 8 }}>
              Tip: weights are typically balanced to 100%
            </Tag>
          )}
        </Text>
      )}
      {canManage && (
        <Button
          type="primary"
          size="small"
          onClick={() => setOpenAddForm(true)}
        >
          Add Criterion
        </Button>
      )}
    </Space>
  )

  return (
    <>
      <WaydGrid
        height={300}
        columns={columns}
        data={sortedCriteria}
        leftSlot={actions}
        onRefresh={loadData}
        csvFileName="scoring-criteria"
      />
      {openAddForm && (
        <AddScoringModelCriterionForm
          scoringModelId={scoringModel.id}
          scales={scoringModel.scales ?? []}
          onFormComplete={() => setOpenAddForm(false)}
          onFormCancel={() => setOpenAddForm(false)}
        />
      )}
      {editingCriterion && (
        <EditScoringModelCriterionForm
          scoringModelId={scoringModel.id}
          scales={scoringModel.scales ?? []}
          criterion={editingCriterion}
          onFormComplete={() => setEditingCriterion(null)}
          onFormCancel={() => setEditingCriterion(null)}
        />
      )}
    </>
  )
}

export default ScoringModelCriteriaList
