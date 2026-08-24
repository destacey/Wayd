'use client'

import { PlanningIntervalObjectiveListDto } from '@/src/services/wayd-api'
import { PlusOutlined } from '@ant-design/icons'
import { Badge, Button, Card, Space } from 'antd'
import ObjectiveListItem from './objective-list-item'
import WaydEmpty from '@/src/components/common/wayd-empty'
import WaydList from '@/src/components/common/wayd-list'
import { useEffect, useState } from 'react'
import CreatePlanningIntervalObjectiveForm from '../../_components/create-planning-interval-objective-form'
import useTheme from '@/src/components/contexts/theme'
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
import { useUpdateObjectivesOrderMutation } from '@/src/store/features/planning/planning-interval-api'
import { useMessage } from '@/src/components/contexts/messaging'

export interface TeamObjectivesListCardProps {
  objectivesData: PlanningIntervalObjectiveListDto[]
  refreshObjectives: () => void
  planningIntervalId: string
  planningIntervalKey: number
  teamId: string
  newObjectivesAllowed?: boolean
  refreshPlanningInterval: () => void
  onObjectiveClick: (objectiveKey: number) => void
  canManageObjectives: boolean
  canCreateHealthChecks: boolean
}

const sortOrderedObjectives = (
  objectivesData: PlanningIntervalObjectiveListDto[],
) => {
  // .slice() is used to prevent: TypeError: Cannot assign to read only property '0' of object '[object Array]'
  return objectivesData.slice().sort((a, b) => {
    if ((a.order ?? null) === null && (b.order ?? null) === null) return a.key - b.key
    if ((a.order ?? null) === null) return 1
    if ((b.order ?? null) === null) return -1
    if (a.order === b.order) return a.key - b.key
    return (a.order ?? 0) - (b.order ?? 0)
  })
}

const TeamObjectivesListCard = ({
  objectivesData,
  refreshObjectives,
  planningIntervalId,
  planningIntervalKey,
  teamId,
  newObjectivesAllowed = false,
  refreshPlanningInterval,
  onObjectiveClick,
  canManageObjectives,
  canCreateHealthChecks,
}: TeamObjectivesListCardProps) => {
  const [openCreateObjectiveForm, setOpenCreateObjectiveForm] =
    useState<boolean>(false)
  const sortedObjectivesData = objectivesData ? sortOrderedObjectives(objectivesData) : []

  const messageApi = useMessage()

  const { badgeColor } = useTheme()

  const [updateObjectivesOrder, { error: updateObjectivesOrderError }] =
    useUpdateObjectivesOrderMutation()

  const [localOverride, setLocalOverride] = useState<{
    data: PlanningIntervalObjectiveListDto[]
    source: PlanningIntervalObjectiveListDto[]
  } | null>(null)

  // Use derived state: discard local override if source data changed or error occurred
  const objectives =
    localOverride &&
    localOverride.source === objectivesData &&
    !updateObjectivesOrderError
      ? localOverride.data
      : sortedObjectivesData

  const setObjectives = (value: PlanningIntervalObjectiveListDto[]) => {
    setLocalOverride({ data: value, source: objectivesData })
  }

  const canCreateObjectives =
    newObjectivesAllowed && planningIntervalId && canManageObjectives

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(TouchSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  )

  useEffect(() => {
    if (!updateObjectivesOrderError) return

    // TODO: show error message not working
    messageApi.error('Error updating objectives order.  Resetting order...')

    console.error(
      'Error updating objectives order:',
      updateObjectivesOrderError,
    )
  }, [messageApi, updateObjectivesOrderError])

  const refresh = () => {
    refreshObjectives()
    // this will update the PI predictability on the plan review page title
    refreshPlanningInterval()
  }

  const cardTitle = (() => {
    const count = objectives.length ?? 0
    const showBadge = count > 0
    return (
      <Space>
        {'Objectives'}
        {showBadge && <Badge color={badgeColor} size="small" count={count} />}
      </Space>
    )
  })()

  const onCreateObjectiveFormClosed = (wasCreated: boolean) => {
    setOpenCreateObjectiveForm(false)
    if (wasCreated) {
      refresh()
    }
  }

  const newObjectiveOrderValue = () => {
    if (!objectives || objectives.length === 0) return undefined

    const lastObjectiveOrder = objectives[objectives.length - 1].order

    return !lastObjectiveOrder ? undefined : lastObjectiveOrder + 1
  }

  const getObjectivePosition = (id: string | undefined) => objectives.findIndex((o) => o.id === id)

  const onDragEnd = (event: DragEndEvent) => {
    const { active, over } = event

    if (active.id === over?.id) return

    const originalPosition = getObjectivePosition(String(active.id))
    const newPosition = getObjectivePosition(over?.id !== undefined ? String(over.id) : undefined)

    const updatedObjectives = arrayMove(
      objectives,
      originalPosition,
      newPosition,
    )

    setObjectives(updatedObjectives)

    // after optimistic update
    const changedObjectivesDictionary: { [key: string]: number } = {}
    updatedObjectives.forEach((o, i) => {
      const position = i + 1
      if (o.order !== position) {
        changedObjectivesDictionary[o.id] = position
      }
    })

    updateObjectivesOrder({
      planningIntervalId: planningIntervalId,
      objectives: changedObjectivesDictionary,
    })
  }

  return (
    <>
      <Card
        size="small"
        title={cardTitle}
        extra={
          canCreateObjectives && (
            <Button
              type="text"
              icon={<PlusOutlined />}
              onClick={() => setOpenCreateObjectiveForm(true)}
            />
          )
        }
        styles={{ body: { padding: 4 } }}
      >
        <DndContext
          sensors={sensors}
          collisionDetection={closestCorners}
          onDragEnd={onDragEnd}
        >
          <WaydList
            size="small"
            dataSource={objectives}
            locale={{
              emptyText: <WaydEmpty message="No objectives" />,
            }}
            renderItem={(objective) => (
              <SortableContext
                items={objectives.map((objective) => objective.id)}
                strategy={verticalListSortingStrategy}
              >
                <ObjectiveListItem
                  objective={objective}
                  piKey={objective.planningInterval.key}
                  canUpdateObjectives={canManageObjectives}
                  canCreateHealthChecks={canCreateHealthChecks}
                  refreshObjectives={refresh}
                  onObjectiveClick={onObjectiveClick}
                />
              </SortableContext>
            )}
          />
        </DndContext>
      </Card>
      {openCreateObjectiveForm && (
        <CreatePlanningIntervalObjectiveForm
          planningIntervalKey={planningIntervalKey}
          teamId={teamId}
          order={newObjectiveOrderValue()}
          onFormCreate={() => onCreateObjectiveFormClosed(true)}
          onFormCancel={() => onCreateObjectiveFormClosed(false)}
        />
      )}
    </>
  )
}

export default TeamObjectivesListCard
