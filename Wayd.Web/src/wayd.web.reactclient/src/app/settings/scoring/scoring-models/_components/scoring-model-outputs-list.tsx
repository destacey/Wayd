'use client'

import { WaydGrid } from '@/src/components/common'
import { RowMenuCellRenderer } from '@/src/components/common/wayd-grid-cell-renderers'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringModelDetailsDto,
  ScoringModelOutputDto,
} from '@/src/services/wayd-api'
import {
  useRemoveScoringModelOutputMutation,
  useReorderScoringModelOutputsMutation,
} from '@/src/store/features/scoring/scoring-models-api'
import { App, Button, Space, Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import { useMemo, useState } from 'react'
import AddScoringModelOutputForm from './add-scoring-model-output-form'
import EditScoringModelOutputForm from './edit-scoring-model-output-form'
import { isApiError, type ApiError } from '@/src/utils'

export interface ScoringModelOutputsListProps {
  scoringModel: ScoringModelDetailsDto
  canManage: boolean
  loadData?: () => void
}

interface RowMenuProps {
  output: ScoringModelOutputDto
  sortedOutputs: ScoringModelOutputDto[]
  onEditClicked: (output: ScoringModelOutputDto) => void
  onDeleteClicked: (output: ScoringModelOutputDto) => void
  onMoveClicked: (output: ScoringModelOutputDto, direction: 'up' | 'down') => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.output) return []

  const index = props.sortedOutputs.findIndex((o) => o.id === props.output.id)
  const isFirst = index === 0
  const isLast = index === props.sortedOutputs.length - 1

  const items: ItemType[] = [
    {
      key: 'edit',
      label: 'Edit',
      onClick: () => props.onEditClicked(props.output),
    },
  ]

  if (!isFirst) {
    items.push({
      key: 'move-up',
      label: 'Move Up',
      onClick: () => props.onMoveClicked(props.output, 'up'),
    })
  }

  if (!isLast) {
    items.push({
      key: 'move-down',
      label: 'Move Down',
      onClick: () => props.onMoveClicked(props.output, 'down'),
    })
  }

  items.push(
    { key: 'divider', type: 'divider' },
    {
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.output),
    },
  )

  return items
}

const ScoringModelOutputsList = ({
  scoringModel,
  canManage,
  loadData,
}: ScoringModelOutputsListProps) => {
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const [openAddForm, setOpenAddForm] = useState(false)
  const [addInitialValues, setAddInitialValues] = useState<
    | { name?: string; token?: string; formula?: string; isPrimary?: boolean }
    | undefined
  >(undefined)
  const [editingOutput, setEditingOutput] =
    useState<ScoringModelOutputDto | null>(null)

  const [removeOutput] = useRemoveScoringModelOutputMutation()
  const [reorderOutputs] = useReorderScoringModelOutputsMutation()

  const sortedOutputs = useMemo(
    () =>
      !scoringModel?.outputs
        ? []
        : [...scoringModel.outputs].sort((a, b) => a.order - b.order),
    [scoringModel?.outputs],
  )

  // Tokens a formula can reference: every criterion token plus every output token.
  const availableTokens = useMemo(
    () => [
      ...(scoringModel?.criteria ?? []).map((c) => c.token),
      ...(scoringModel?.outputs ?? []).map((o) => o.token),
    ],
    [scoringModel?.criteria, scoringModel?.outputs],
  )

  const columnDefs = useMemo<ColDef<ScoringModelOutputDto>[]>(() => {
    const handleEdit = (output: ScoringModelOutputDto) => {
      setEditingOutput(output)
    }

    const handleDelete = (output: ScoringModelOutputDto) => {
      modal.confirm({
        title: 'Are you sure you want to delete this output?',
        content: `${output.name} = ${output.formula}`,
        okText: 'Delete',
        okType: 'danger',
        onOk: async () => {
          try {
            const response = await removeOutput({
              scoringModelId: scoringModel.id,
              outputId: output.id,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Output deleted successfully.')
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            messageApi.error(
              apiError.detail ??
                'An unexpected error occurred while deleting the output.',
            )
            console.log(error)
          }
        },
      })
    }

    const handleMove = async (
      output: ScoringModelOutputDto,
      direction: 'up' | 'down',
    ) => {
      const ordered = [...sortedOutputs]
      const index = ordered.findIndex((o) => o.id === output.id)
      if (index < 0) return

      const swapIndex = direction === 'up' ? index - 1 : index + 1
      if (swapIndex < 0 || swapIndex >= ordered.length) return

      ;[ordered[index], ordered[swapIndex]] = [
        ordered[swapIndex],
        ordered[index],
      ]

      try {
        const response = await reorderOutputs({
          scoringModelId: scoringModel.id,
          orderedOutputIds: ordered.map((o) => o.id),
        })
        if (response.error) {
          throw response.error
        }
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ?? 'An error occurred while reordering outputs.',
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
        cellRenderer: (params: ICellRendererParams<ScoringModelOutputDto>) => {
          const menuItems = getRowMenuItems({
            output: params.data!,
            sortedOutputs,
            onEditClicked: handleEdit,
            onDeleteClicked: handleDelete,
            onMoveClicked: handleMove,
          })
          if (menuItems.length === 0) return null
          return RowMenuCellRenderer({ ...params, menuItems })
        },
      },
      { field: 'order', headerName: 'Order', width: 90, sort: 'asc' as const },
      { field: 'name', headerName: 'Name', width: 180 },
      { field: 'token', headerName: 'Token', width: 110 },
      { field: 'formula', headerName: 'Formula', flex: 1 },
      {
        field: 'isPrimary',
        headerName: 'Primary',
        width: 110,
        cellRenderer: (params: ICellRendererParams<ScoringModelOutputDto>) =>
          params.value ? <Tag color="blue">Primary</Tag> : null,
      },
    ]
  }, [
    canManage,
    sortedOutputs,
    modal,
    removeOutput,
    reorderOutputs,
    scoringModel.id,
    messageApi,
  ])

  // Build a normalized weighted-sum formula from criteria that have weights:
  // (T1*w1 + T2*w2 + ...) / (w1 + w2 + ...). Scaffolding only — fully editable afterward.
  const weightedScaffold = useMemo(() => {
    const weighted = (scoringModel?.criteria ?? []).filter(
      (c) => c.weight != null && c.weight > 0,
    )
    if (weighted.length < 2) return null

    const totalWeight = weighted.reduce((sum, c) => sum + (c.weight ?? 0), 0)
    const numerator = weighted
      .map((c) => `${c.token} * ${c.weight}`)
      .join(' + ')
    return `(${numerator}) / ${totalWeight}`
  }, [scoringModel?.criteria])

  const openWeightedScaffold = () => {
    setAddInitialValues({
      name: 'Weighted Score',
      token: 'Score',
      formula: weightedScaffold ?? '',
      isPrimary: true,
    })
    setOpenAddForm(true)
  }

  const openBlankAdd = () => {
    setAddInitialValues(undefined)
    setOpenAddForm(true)
  }

  const closeAddForm = () => {
    setOpenAddForm(false)
    setAddInitialValues(undefined)
  }

  const actions = (
    <Space>
      {canManage && weightedScaffold && (
        <Button
          size="small"
          onClick={openWeightedScaffold}
          title="Pre-fill a weighted-sum formula from the criteria weights. You can edit it afterward."
        >
          Generate Weighted Formula
        </Button>
      )}
      {canManage && (
        <Button type="primary" size="small" onClick={openBlankAdd}>
          Add Output
        </Button>
      )}
    </Space>
  )

  return (
    <>
      <WaydGrid
        height={300}
        columnDefs={columnDefs}
        rowData={sortedOutputs}
        actions={actions}
        loadData={loadData}
      />
      {openAddForm && (
        <AddScoringModelOutputForm
          scoringModelId={scoringModel.id}
          availableTokens={availableTokens}
          initialValues={addInitialValues}
          onFormComplete={closeAddForm}
          onFormCancel={closeAddForm}
        />
      )}
      {editingOutput && (
        <EditScoringModelOutputForm
          scoringModelId={scoringModel.id}
          output={editingOutput}
          availableTokens={availableTokens}
          onFormComplete={() => setEditingOutput(null)}
          onFormCancel={() => setEditingOutput(null)}
        />
      )}
    </>
  )
}

export default ScoringModelOutputsList
