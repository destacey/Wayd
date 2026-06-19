'use client'

import { WaydGrid } from '@/src/components/common'
import { RowMenuCellRenderer } from '@/src/components/common/wayd-grid-cell-renderers'
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
import { ColDef, ICellRendererParams } from 'ag-grid-community'
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

  const columnDefs = useMemo<ColDef<ScoringModelCriterionDto>[]>(() => {
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
      {
        width: 50,
        filter: false,
        sortable: false,
        resizable: false,
        hide: !canManage,
        suppressHeaderMenuButton: true,
        cellRenderer: (
          params: ICellRendererParams<ScoringModelCriterionDto>,
        ) => {
          const menuItems = getRowMenuItems({
            criterion: params.data!,
            sortedCriteria,
            onEditClicked: handleEdit,
            onDeleteClicked: handleDelete,
            onMoveClicked: handleMove,
          })
          if (menuItems.length === 0) return null
          return RowMenuCellRenderer({ ...params, menuItems })
        },
      },
      { field: 'order', headerName: 'Order', width: 80, sort: 'asc' as const },
      { field: 'name', headerName: 'Name', width: 250 },
      { field: 'token', headerName: 'Token', width: 110 },
      {
        headerName: 'Scale',
        valueGetter: (p) => {
          const scaleId = p.data?.scaleId
          if (!scaleId) return 'Free entry'
          return scoringModel.scales?.find((s) => s.id === scaleId)?.name ?? '—'
        },
      },
      {
        field: 'weight',
        headerName: 'Weight (%)',
        width: 120,
        valueFormatter: (p) => (p.value != null ? `${p.value}%` : ''),
      },
      { field: 'description', headerName: 'Description', width: 300 },
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
        columnDefs={columnDefs}
        rowData={sortedCriteria}
        actions={actions}
        loadData={loadData}
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
