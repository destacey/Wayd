import { WaydEmpty, WaydList } from '@/src/components/common'
import { WorkTypeLevelDto, WorkTypeTierDto } from '@/src/services/wayd-api'
import { Button, Card, Typography } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { CreateWorkTypeLevelForm, WorkTypeLevelCard } from '.'
import { PlusOutlined } from '@ant-design/icons'
import {
  DndContext,
  DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  TouchSensor,
  closestCorners,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { useUpdateWorkTypeLevelsOrderMutation } from '@/src/store/features/work-management/work-type-level-api'
import { WorkTypeTier } from '@/src/components/types'

const { Text } = Typography

interface WorkTypeTierCardProps {
  tier: WorkTypeTierDto
  levels: WorkTypeLevelDto[]
  refreshLevels: () => void
  canCreateWorkTypeLevels: boolean
  canUpdateWorkTypeLevels: boolean
}

const sortOrderedLevels = (levels: WorkTypeLevelDto[]) => {
  return levels.slice().sort((a, b) => {
    return a.order - b.order
  })
}

const WorkTypeTierCard = (props: WorkTypeTierCardProps) => {
  const sortedLevels = useMemo(
    () => (props.levels ? sortOrderedLevels(props.levels) : []),
    [props.levels],
  )

  // Initialize with sorted levels and update when levels change from external source
  const [orderedLevels, setOrderedLevels] =
    useState<WorkTypeLevelDto[]>(sortedLevels)
  const [openCreateWorkTypeLevelForm, setOpenCreateWorkTypeLevelForm] =
    useState<boolean>(false)

  const canOrder = props.tier.id === WorkTypeTier.Portfolio

  const canCreateNewLevel = canOrder && props.canCreateWorkTypeLevels

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(TouchSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  )

  const [updateLevelsOrder] = useUpdateWorkTypeLevelsOrderMutation()

  // Sync orderedLevels when sortedLevels changes from external updates (e.g., after create/delete)
  useEffect(() => {
    setOrderedLevels(sortedLevels)
  }, [sortedLevels])

  const onCreateObjectiveFormClosed = (wasSaved: boolean) => {
    setOpenCreateWorkTypeLevelForm(false)
    if (wasSaved) {
      props.refreshLevels()
    }
  }

  const getLevelPosition = (id: string | undefined) => orderedLevels.findIndex((o) => String(o.id) === id)

  const onDragEnd = (event: DragEndEvent) => {
    if (!canOrder) return

    const { active, over } = event

    if (active.id === over?.id) return

    const originalPosition = getLevelPosition(String(active.id))
    const newPosition = getLevelPosition(over?.id !== undefined ? String(over.id) : undefined)

    const updatedLevels = arrayMove(
      orderedLevels,
      originalPosition,
      newPosition,
    )

    setOrderedLevels(updatedLevels)

    // after optimistic update
    const changedLevelsDictionary: { [key: string]: number } = {}
    updatedLevels.forEach((o, i) => {
      const position = i + 1
      if (o.order !== position) {
        changedLevelsDictionary[o.id] = position
      }
    })

    updateLevelsOrder({
      levels: changedLevelsDictionary,
    })
  }

  return (
    <>
      <Card
        size="small"
        title={props.tier.name}
        extra={
          canCreateNewLevel && (
            <Button
              type="text"
              title="Create new work type level"
              icon={<PlusOutlined />}
              onClick={() => setOpenCreateWorkTypeLevelForm(true)}
            />
          )
        }
      >
        <Text>{props.tier.description}</Text>
        <br />
        <br />
        <DndContext
          sensors={sensors}
          collisionDetection={closestCorners}
          onDragEnd={onDragEnd}
        >
          <WaydList
            size="small"
            style={{ width: '100%' }}
            dataSource={orderedLevels}
            locale={{
              emptyText: <WaydEmpty message="No work type levels" />,
            }}
            renderItem={(level) => (
              <SortableContext
                items={orderedLevels.map((level) => level.id)}
                strategy={verticalListSortingStrategy}
              >
                <WorkTypeLevelCard
                  level={level}
                  canUpdateWorkTypeLevels={props.canUpdateWorkTypeLevels}
                  canOrder={canOrder}
                  refreshLevels={props.refreshLevels}
                />
              </SortableContext>
            )}
          />
        </DndContext>
      </Card>
      {openCreateWorkTypeLevelForm && (
        <CreateWorkTypeLevelForm
          onFormSave={() => onCreateObjectiveFormClosed(true)}
          onFormCancel={() => onCreateObjectiveFormClosed(false)}
        />
      )}
    </>
  )
}

export default WorkTypeTierCard
