'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringModelDetailsDto,
  ScoringRatingLevelDto,
  ScoringScaleDto,
} from '@/src/services/wayd-api'
import {
  useRemoveScoringScaleMutation,
  useRemoveScoringScaleLevelMutation,
  useReorderScoringScaleLevelsMutation,
} from '@/src/store/features/scoring/scoring-models-api'
import { App, Button, Collapse, Empty, Space, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import type { ColumnDef } from '@tanstack/react-table'
import { useMemo, useState } from 'react'
import AddScoringScaleForm from './add-scoring-scale-form'
import EditScoringScaleForm from './edit-scoring-scale-form'
import AddScoringScaleLevelForm from './add-scoring-scale-level-form'
import EditScoringScaleLevelForm from './edit-scoring-scale-level-form'
import { isApiError, type ApiError } from '@/src/utils'

const { Text } = Typography

export interface ScoringScalesListProps {
  scoringModel: ScoringModelDetailsDto
  canManage: boolean
  loadData?: () => void
}

const ScoringScalesList = ({
  scoringModel,
  canManage,
  loadData,
}: ScoringScalesListProps) => {
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const [openAddScale, setOpenAddScale] = useState(false)
  const [editingScale, setEditingScale] = useState<ScoringScaleDto | null>(null)
  const [addLevelScaleId, setAddLevelScaleId] = useState<string | null>(null)
  const [editingLevel, setEditingLevel] = useState<{
    scaleId: string
    level: ScoringRatingLevelDto
  } | null>(null)

  const [removeScale] = useRemoveScoringScaleMutation()
  const [removeLevel] = useRemoveScoringScaleLevelMutation()
  const [reorderLevels] = useReorderScoringScaleLevelsMutation()

  const sortedScales = useMemo(
    () =>
      !scoringModel?.scales
        ? []
        : [...scoringModel.scales].sort((a, b) => a.order - b.order),
    [scoringModel?.scales],
  )

  const handleDeleteScale = (scale: ScoringScaleDto) => {
    modal.confirm({
      title: 'Delete this scale?',
      content: scale.name,
      okText: 'Delete',
      okType: 'danger',
      onOk: async () => {
        try {
          const response = await removeScale({
            scoringModelId: scoringModel.id,
            scaleId: scale.id,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Scale deleted successfully.')
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          messageApi.error(
            apiError.detail ??
              'An unexpected error occurred while deleting the scale.',
          )
        }
      },
    })
  }

  const makeLevelColumns = (
    scale: ScoringScaleDto,
    sortedLevels: ScoringRatingLevelDto[],
  ): ColumnDef<ScoringRatingLevelDto, any>[] => {
    const handleDeleteLevel = (level: ScoringRatingLevelDto) => {
      modal.confirm({
        title: 'Delete this rating level?',
        content: `${level.label} (${level.value})`,
        okText: 'Delete',
        okType: 'danger',
        onOk: async () => {
          try {
            const response = await removeLevel({
              scoringModelId: scoringModel.id,
              scaleId: scale.id,
              levelId: level.id,
            })
            if (response.error) {
              throw response.error
            }
            messageApi.success('Rating level deleted successfully.')
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            messageApi.error(
              apiError.detail ??
                'An unexpected error occurred while deleting the rating level.',
            )
          }
        },
      })
    }

    const handleMoveLevel = async (
      level: ScoringRatingLevelDto,
      direction: 'up' | 'down',
    ) => {
      const ordered = [...sortedLevels]
      const index = ordered.findIndex((l) => l.id === level.id)
      if (index < 0) return
      const swapIndex = direction === 'up' ? index - 1 : index + 1
      if (swapIndex < 0 || swapIndex >= ordered.length) return
      ;[ordered[index], ordered[swapIndex]] = [ordered[swapIndex], ordered[index]]

      try {
        const response = await reorderLevels({
          scoringModelId: scoringModel.id,
          scaleId: scale.id,
          orderedLevelIds: ordered.map((l) => l.id),
        })
        if (response.error) {
          throw response.error
        }
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while reordering rating levels.',
        )
      }
    }

    const getRowMenuItems = (
      level: ScoringRatingLevelDto,
    ): ItemType[] => {
      const index = sortedLevels.findIndex((l) => l.id === level.id)
      const items: ItemType[] = [
        {
          key: 'edit',
          label: 'Edit',
          onClick: () => setEditingLevel({ scaleId: scale.id, level }),
        },
      ]
      if (index > 0) {
        items.push({
          key: 'move-up',
          label: 'Move Up',
          onClick: () => handleMoveLevel(level, 'up'),
        })
      }
      if (index < sortedLevels.length - 1) {
        items.push({
          key: 'move-down',
          label: 'Move Down',
          onClick: () => handleMoveLevel(level, 'down'),
        })
      }
      items.push(
        { key: 'divider', type: 'divider' },
        {
          key: 'delete',
          label: 'Delete',
          danger: true,
          onClick: () => handleDeleteLevel(level),
        },
      )
      return items
    }

    return [
      createActionsColumn<ScoringRatingLevelDto>({
        hide: !canManage,
        ariaLabel: 'Rating level actions',
        getItems: (level) => getRowMenuItems(level),
      }),
      {
        id: 'order',
        accessorKey: 'order',
        header: 'Order',
        size: 90,
        enableColumnFilter: false,
      },
      { id: 'label', accessorKey: 'label', header: 'Label', size: 200 },
      { id: 'value', accessorKey: 'value', header: 'Value', size: 150 },
    ]
  }

  const collapseItems = sortedScales.map((scale) => {
    const sortedLevels = [...scale.levels].sort((a, b) => a.order - b.order)
    return {
      key: scale.id,
      label: (
        <Space>
          <Text strong>{scale.name}</Text>
          <Text type="secondary">
            ({scale.levels.length} level{scale.levels.length === 1 ? '' : 's'})
          </Text>
        </Space>
      ),
      extra: canManage ? (
        <Space onClick={(e) => e.stopPropagation()}>
          <Button size="small" onClick={() => setAddLevelScaleId(scale.id)}>
            Add Level
          </Button>
          <Button size="small" onClick={() => setEditingScale(scale)}>
            Rename
          </Button>
          <Button size="small" danger onClick={() => handleDeleteScale(scale)}>
            Delete
          </Button>
        </Space>
      ) : undefined,
      children: (
        <WaydGrid
          height={220}
          columns={makeLevelColumns(scale, sortedLevels)}
          data={sortedLevels}
          onRefresh={loadData}
          persistStateKey="settings-scoring-scale-levels"
          csvFileName="scoring-scale-levels"
        />
      ),
    }
  })

  return (
    <>
      <Space orientation="vertical" style={{ width: '100%' }}>
        {canManage && (
          <Button type="primary" size="small" onClick={() => setOpenAddScale(true)}>
            Add Scale
          </Button>
        )}
        {sortedScales.length === 0 ? (
          <Empty description="No scales. Criteria without a scale are rated by free numeric entry." />
        ) : (
          <Collapse items={collapseItems} defaultActiveKey={sortedScales.map((s) => s.id)} />
        )}
      </Space>

      {openAddScale && (
        <AddScoringScaleForm
          scoringModelId={scoringModel.id}
          onFormComplete={() => setOpenAddScale(false)}
          onFormCancel={() => setOpenAddScale(false)}
        />
      )}
      {editingScale && (
        <EditScoringScaleForm
          scoringModelId={scoringModel.id}
          scale={editingScale}
          onFormComplete={() => setEditingScale(null)}
          onFormCancel={() => setEditingScale(null)}
        />
      )}
      {addLevelScaleId && (
        <AddScoringScaleLevelForm
          scoringModelId={scoringModel.id}
          scaleId={addLevelScaleId}
          onFormComplete={() => setAddLevelScaleId(null)}
          onFormCancel={() => setAddLevelScaleId(null)}
        />
      )}
      {editingLevel && (
        <EditScoringScaleLevelForm
          scoringModelId={scoringModel.id}
          scaleId={editingLevel.scaleId}
          level={editingLevel.level}
          onFormComplete={() => setEditingLevel(null)}
          onFormCancel={() => setEditingLevel(null)}
        />
      )}
    </>
  )
}

export default ScoringScalesList
