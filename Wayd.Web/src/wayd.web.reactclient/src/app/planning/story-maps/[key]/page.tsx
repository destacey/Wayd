'use client'

import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import { getAvatarColor } from '@/src/utils'
import {
  useStoryMapConnection,
  PresenceParticipant,
} from '@/src/hooks/use-story-map-connection'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import useTheme from '@/src/components/contexts/theme'
import {
  useGetStoryMapQuery,
  useAddGoalMutation,
  useRenameGoalMutation,
  useDeleteGoalMutation,
  useAddStepMutation,
  useRenameStepMutation,
  useDeleteStepMutation,
  useAddTaskMutation,
  useUpdateTaskMutation,
  useDeleteTaskMutation,
  useAddSwimLaneMutation,
  useRenameSwimLaneMutation,
  useRemoveSwimLaneMutation,
  useSetSwimLaneDatesMutation,
  useReorderGoalMutation,
  useReorderStepMutation,
  useMoveStepMutation,
  useMoveTaskMutation,
  useReorderSwimLaneMutation,
  useSetStepPersonasMutation,
  useSetTaskPersonasMutation,
} from '@/src/store/features/planning/story-maps-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { Avatar, Button, Divider, Dropdown, Flex, Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { WaydTooltip } from '@/src/components/common'
import { MoreOutlined, PlusOutlined } from '@ant-design/icons'
import { notFound, useParams, usePathname, useRouter } from 'next/navigation'
import {
  CSSProperties,
  FC,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import PageTitle from '@/src/components/common/page-title'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { togglePersonaId } from '@/src/store/features/planning/story-map-patches'
import { generateCsv, downloadCsvWithTimestamp } from '@/src/utils/csv-utils'
import type { DropResult } from './_components/board-drag'
import { buildExportRows, EXPORT_HEADERS } from './_components/board-export'
import {
  BoardActions,
  EditStoryMapForm,
  ManagePersonasForm,
  PersonaFilterBar,
  StoryMapBoard,
} from './_components'
import { ArchiveStoryMapForm, DeleteStoryMapForm } from '../_components'
import StoryMapDetailsLoading from './loading'
import styles from '../_components/story-map.module.css'

const { Group: AvatarGroup } = Avatar

/** Names the moved node in the failure toast. */
const DROP_LABELS: Record<DropResult['kind'], string> = {
  goal: 'goal',
  step: 'step',
  task: 'task',
  swimLane: 'swim lane',
}

const DEFAULT_GOAL_NAME = 'New goal'
const DEFAULT_STEP_NAME = 'New step'
const DEFAULT_TASK_TITLE = 'New task'

interface StoryMapCssVars extends CSSProperties {
  '--sm-bg': string
  '--sm-bg-elevated': string
  '--sm-border': string
  '--sm-text': string
  '--sm-text-secondary': string
  '--sm-muted': string
  '--sm-radius': string
  '--sm-radius-lg': string
}

const StoryMapDetailPage: FC = () => {
  const { key } = useParams<{ key: string }>()
  const pathname = usePathname()
  const router = useRouter()
  const dispatch = useAppDispatch()
  useDocumentTitle(`Story Map ${key}`)

  const { token } = useTheme()
  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.StoryMaps.Update')
  const canDelete = hasPermissionClaim('Permissions.StoryMaps.Delete')

  const { data: map, isLoading } = useGetStoryMapQuery(key)

  const [presence, setPresence] = useState<PresenceParticipant[]>([])

  const [addGoal] = useAddGoalMutation()
  const [renameGoal] = useRenameGoalMutation()
  const [deleteGoal] = useDeleteGoalMutation()
  const [addStep] = useAddStepMutation()
  const [renameStep] = useRenameStepMutation()
  const [deleteStep] = useDeleteStepMutation()
  const [addTask] = useAddTaskMutation()
  const [updateTask] = useUpdateTaskMutation()
  const [deleteTask] = useDeleteTaskMutation()
  const [addSwimLane] = useAddSwimLaneMutation()
  const [renameSwimLane] = useRenameSwimLaneMutation()
  const [removeSwimLane] = useRemoveSwimLaneMutation()
  const [setSwimLaneDates] = useSetSwimLaneDatesMutation()
  const [reorderGoal] = useReorderGoalMutation()
  const [reorderStep] = useReorderStepMutation()
  const [moveStep] = useMoveStepMutation()
  const [moveTask] = useMoveTaskMutation()
  const [reorderSwimLane] = useReorderSwimLaneMutation()
  const [setStepPersonas] = useSetStepPersonasMutation()
  const [setTaskPersonas] = useSetTaskPersonasMutation()

  const [openEditForm, setOpenEditForm] = useState(false)
  const [openArchiveMap, setOpenArchiveMap] = useState(false)
  const [openDeleteMap, setOpenDeleteMap] = useState(false)
  const [selectedPersonaId, setSelectedPersonaId] = useState<string | null>(
    null,
  )
  const [openManagePersonas, setOpenManagePersonas] = useState(false)
  // The id of an item just created inline, so its name field opens in edit mode.
  const [autoEditId, setAutoEditId] = useState<string | null>(null)

  const messageApi = useMessage()

  // The hub broadcasts MapDeleted to the whole group, including whoever pressed delete. They already
  // have a success toast and are already navigating, so only tell the others.
  const deletedHereRef = useRef(false)

  // Someone else deleted the map while we were looking at it. Staying here would leave a board whose
  // every edit silently fails, so say what happened and return to the list.
  const handleMapDeleted = useCallback(() => {
    if (deletedHereRef.current) return
    messageApi.warning('This story map was deleted.')
    router.push('/planning/story-maps')
  }, [messageApi, router])

  useStoryMapConnection(map?.id, setPresence, handleMapDeleted)

  useEffect(() => {
    if (!map) return
    dispatch(setBreadcrumbTitle({ title: map.name, pathname }))
  }, [dispatch, pathname, map])

  // Step and task footers show one toggle dot per persona, in the same order as the filter bar.
  const orderedPersonas = useMemo(
    () => [...(map?.personas ?? [])].sort((a, b) => a.order - b.order),
    [map],
  )


  const handleAddGoal = async () => {
    if (!map) return
    try {
      const goal = await addGoal({
        storyMapId: map.id,
        request: { name: DEFAULT_GOAL_NAME },
      }).unwrap()
      setAutoEditId(goal.id)
    } catch {
      messageApi.error('Failed to add goal.')
    }
  }

  const handleAddStep = async (goalId: string) => {
    if (!map) return
    try {
      const step = await addStep({
        storyMapId: map.id,
        request: { goalId, name: DEFAULT_STEP_NAME },
      }).unwrap()
      setAutoEditId(step.id)
    } catch {
      messageApi.error('Failed to add step.')
    }
  }

  const handleAddTask = async (stepId: string) => {
    if (!map) return
    try {
      const task = await addTask({
        storyMapId: map.id,
        request: {
          stepId,
          title: DEFAULT_TASK_TITLE,
          swimLaneId: map.swimLanes.find((l) => l.isDefault)?.id,
        },
      }).unwrap()
      setAutoEditId(task.id)
    } catch {
      messageApi.error('Failed to add task.')
    }
  }

  const handleAddSwimLane = async () => {
    if (!map) return
    try {
      const lane = await addSwimLane({
        storyMapId: map.id,
        request: { name: `Swim lane ${map.swimLanes.length + 1}` },
      }).unwrap()
      setAutoEditId(lane.id)
    } catch {
      messageApi.error('Failed to add swim lane.')
    }
  }

  const handleRenameSwimLane = async (swimLaneId: string, name: string) => {
    if (!map) return
    try {
      await renameSwimLane({
        storyMapId: map.id,
        storyMapKey: key,
        swimLaneId,
        request: { name },
      }).unwrap()
    } catch {
      messageApi.error('Failed to rename swim lane.')
    }
  }

  // A completed drag. board-drag.ts has already worked out which node moved and where; this only
  // picks the matching mutation. Each one patches the cache optimistically and rolls back on error.
  const handleDrop = async (drop: DropResult) => {
    if (!map) return
    const common = { storyMapId: map.id, storyMapKey: key }

    try {
      switch (drop.kind) {
        case 'goal':
          await reorderGoal({
            ...common,
            goalId: drop.goalId,
            newOrder: drop.newOrder,
          }).unwrap()
          break

        case 'step':
          // Same goal is a reorder; a different goal is a move.
          if (drop.targetGoalId) {
            await moveStep({
              ...common,
              stepId: drop.stepId,
              request: {
                targetGoalId: drop.targetGoalId,
                newOrder: drop.newOrder,
              },
            }).unwrap()
          } else {
            await reorderStep({
              ...common,
              stepId: drop.stepId,
              newOrder: drop.newOrder,
            }).unwrap()
          }
          break

        case 'task':
          // There is no reorder endpoint for tasks — moveTask covers both cases.
          await moveTask({
            ...common,
            taskId: drop.taskId,
            request: {
              targetStepId: drop.targetStepId,
              targetSwimLaneId: drop.targetSwimLaneId,
              newOrder: drop.newOrder,
            },
          }).unwrap()
          break

        case 'swimLane':
          await reorderSwimLane({
            ...common,
            swimLaneId: drop.swimLaneId,
            newOrder: drop.newOrder,
          }).unwrap()
          break
      }
    } catch {
      messageApi.error(`Failed to move ${DROP_LABELS[drop.kind]}.`)
    }
  }

  const handleSetSwimLaneDates = async (
    swimLaneId: string,
    startDate: Date | undefined,
    endDate: Date | undefined,
  ) => {
    if (!map) return
    try {
      await setSwimLaneDates({
        storyMapId: map.id,
        storyMapKey: key,
        swimLaneId,
        request: { startDate, endDate },
      }).unwrap()
    } catch {
      messageApi.error('Failed to update swim lane dates.')
    }
  }

  const handleDeleteSwimLane = async (swimLaneId: string) => {
    if (!map) return
    try {
      const movedCount = await removeSwimLane({
        storyMapId: map.id,
        storyMapKey: key,
        swimLaneId,
      }).unwrap()
      // The lane's tasks are not deleted — say where they went.
      if (movedCount > 0) {
        messageApi.success(
          `Swim lane deleted. ${movedCount} ${
            movedCount === 1 ? 'task' : 'tasks'
          } moved to the default lane.`,
        )
      }
    } catch {
      messageApi.error('Failed to delete swim lane.')
    }
  }

  const handleRenameGoal = async (goalId: string, name: string) => {
    if (!map) return
    try {
      await renameGoal({
        storyMapId: map.id,
        storyMapKey: key,
        goalId,
        request: { name },
      }).unwrap()
    } catch {
      messageApi.error('Failed to rename goal.')
    }
  }

  const handleDeleteGoal = async (goalId: string) => {
    if (!map) return
    try {
      await deleteGoal({ storyMapId: map.id, storyMapKey: key, goalId }).unwrap()
    } catch {
      messageApi.error('Failed to delete goal.')
    }
  }

  const handleRenameStep = async (stepId: string, name: string) => {
    if (!map) return
    try {
      await renameStep({
        storyMapId: map.id,
        storyMapKey: key,
        stepId,
        request: { name },
      }).unwrap()
    } catch {
      messageApi.error('Failed to rename step.')
    }
  }

  const handleDeleteStep = async (stepId: string) => {
    if (!map) return
    try {
      await deleteStep({ storyMapId: map.id, storyMapKey: key, stepId }).unwrap()
    } catch {
      messageApi.error('Failed to delete step.')
    }
  }

  const handleRenameTask = async (task: StoryMapTaskDto, title: string) => {
    if (!map) return
    try {
      await updateTask({
        storyMapId: map.id,
        storyMapKey: key,
        taskId: task.id,
        request: { title, description: task.description },
      }).unwrap()
    } catch {
      messageApi.error('Failed to rename task.')
    }
  }

  const handleDeleteTask = async (taskId: string) => {
    if (!map) return
    try {
      await deleteTask({ storyMapId: map.id, storyMapKey: key, taskId }).unwrap()
    } catch {
      messageApi.error('Failed to delete task.')
    }
  }

  const handleToggleStepPersona = async (stepId: string, personaId: string) => {
    if (!map) return
    const step = map.goals
      .flatMap((goal) => goal.steps)
      .find((s) => s.id === stepId)
    if (!step) return

    try {
      await setStepPersonas({
        storyMapId: map.id,
        storyMapKey: key,
        stepId,
        request: { personaIds: togglePersonaId(step.personaIds, personaId) },
      }).unwrap()
    } catch {
      messageApi.error('Failed to update step personas.')
    }
  }

  const handleToggleTaskPersona = async (taskId: string, personaId: string) => {
    if (!map) return
    const task = map.goals
      .flatMap((goal) => goal.steps)
      .flatMap((step) => step.tasks)
      .find((t) => t.id === taskId)
    if (!task) return

    try {
      await setTaskPersonas({
        storyMapId: map.id,
        storyMapKey: key,
        taskId,
        request: { personaIds: togglePersonaId(task.personaIds, personaId) },
      }).unwrap()
    } catch {
      messageApi.error('Failed to update task personas.')
    }
  }

  if (isLoading) {
    return <StoryMapDetailsLoading />
  }

  if (!map) {
    return notFound()
  }

  const isActive = map.status === 'Active'
  const canEdit = canUpdate && isActive

  const cssVars: StoryMapCssVars = {
    '--sm-bg': token.colorBgContainer,
    '--sm-bg-elevated': token.colorFillQuaternary,
    '--sm-border': token.colorBorderSecondary,
    '--sm-text': token.colorText,
    '--sm-text-secondary': token.colorTextSecondary,
    '--sm-muted': token.colorTextTertiary,
    '--sm-radius': `${token.borderRadius}px`,
    '--sm-radius-lg': `${token.borderRadiusLG}px`,
  }

  const statusColor: Record<string, string> = {
    Active: 'processing',
    Archived: 'default',
  }

  const pageTitleTags = (
    <Flex align="center" gap={8} wrap>
      <Tag color={statusColor[map.status] ?? 'default'}>{map.status}</Tag>
    </Flex>
  )

  const handleExportCsv = () => {
    const csv = generateCsv(EXPORT_HEADERS, buildExportRows(map))
    downloadCsvWithTimestamp(csv, `story-map-${map.key}`)
  }

  const menuItems: ItemType[] = [
    ...(canEdit
      ? [
          {
            key: 'edit',
            label: 'Edit',
            onClick: () => setOpenEditForm(true),
          },
          {
            key: 'archive',
            label: 'Archive',
            onClick: () => setOpenArchiveMap(true),
          },
        ]
      : []),
    ...(canDelete
      ? [
          {
            key: 'delete',
            label: 'Delete',
            danger: true,
            onClick: () => setOpenDeleteMap(true),
          },
        ]
      : []),
    // Only separate the export once something sits above it — a viewer with neither permission sees
    // export alone, and a leading divider would hang off the top of the menu.
    ...(canEdit || canDelete
      ? [{ key: 'export-divider', type: 'divider' as const }]
      : []),
    // Viewing is enough to export — it reads nothing the page is not already showing.
    {
      key: 'export',
      label: 'Export CSV',
      onClick: handleExportCsv,
    },
  ]

  const pageTitleActions = (
    <Flex align="center" gap={8} wrap>
      {presence.length > 0 && (
        <AvatarGroup
          max={{
            count: 5,
            style: { backgroundColor: token.colorPrimary, fontSize: 12 },
          }}
          size="small"
        >
          {presence.map((p) => (
            <WaydTooltip key={p.id} title={p.name}>
              <Avatar
                size="small"
                style={{ backgroundColor: getAvatarColor(p.id) }}
              >
                {p.name.charAt(0).toUpperCase()}
              </Avatar>
            </WaydTooltip>
          ))}
        </AvatarGroup>
      )}
      {canEdit && (
        <Button type="primary" icon={<PlusOutlined />} onClick={handleAddGoal}>
          Goal
        </Button>
      )}
      <Dropdown menu={{ items: menuItems }} trigger={['click']}>
        <Button icon={<MoreOutlined />} aria-label="More actions" />
      </Dropdown>
    </Flex>
  )

  const actions: BoardActions = {
    canUpdate: canEdit,
    autoEditId,
    onAutoEditEnd: () => setAutoEditId(null),
    personas: orderedPersonas,
    onRenameGoal: handleRenameGoal,
    onDeleteGoal: handleDeleteGoal,
    onRenameStep: handleRenameStep,
    onDeleteStep: handleDeleteStep,
    onAddTask: handleAddTask,
    onRenameTask: handleRenameTask,
    onDeleteTask: handleDeleteTask,
    onToggleStepPersona: handleToggleStepPersona,
    onToggleTaskPersona: handleToggleTaskPersona,
    onRenameSwimLane: handleRenameSwimLane,
    onDeleteSwimLane: handleDeleteSwimLane,
    onSetSwimLaneDates: handleSetSwimLaneDates,
    onDrop: handleDrop,
  }

  return (
    <div className={styles.pageContainer} style={cssVars}>
      <PageTitle
        title={map.name}
        subtitle="Story Map"
        tags={pageTitleTags}
        actions={pageTitleActions}
      />

      <Divider className={styles.headerDivider} />

      <PersonaFilterBar
        storyMapId={map.id}
        storyMapKey={key}
        map={map}
        personas={map.personas}
        selectedPersonaId={selectedPersonaId}
        onSelectPersona={setSelectedPersonaId}
        canUpdate={canEdit}
        onManage={() => setOpenManagePersonas(true)}
      />

      {map.goals.length === 0 ? (
        <div className={styles.emptyBoard}>
          <div className={styles.emptyBoardTile} aria-hidden>
            <PlusOutlined />
          </div>
          <span className={styles.emptyBoardTitle}>Start with a goal</span>
          <span className={styles.emptyBoardText}>
            Goals are what your users are trying to accomplish. Add the first
            one to begin mapping the journey.
          </span>
          {canEdit && (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={handleAddGoal}
            >
              Add goal
            </Button>
          )}
        </div>
      ) : (
        <StoryMapBoard
          map={map}
          selectedPersonaId={selectedPersonaId}
          actions={actions}
          onAddStep={handleAddStep}
          onAddSwimLane={handleAddSwimLane}
        />
      )}

      {openEditForm && (
        <EditStoryMapForm
          storyMapKey={key}
          onFormUpdate={() => setOpenEditForm(false)}
          onFormCancel={() => setOpenEditForm(false)}
        />
      )}
      {openManagePersonas && (
        <ManagePersonasForm
          map={map}
          storyMapKey={key}
          onClose={() => setOpenManagePersonas(false)}
        />
      )}
      {openArchiveMap && (
        <ArchiveStoryMapForm
          storyMap={{ id: map.id, key: map.key, name: map.name }}
          onFormComplete={() => setOpenArchiveMap(false)}
          onFormCancel={() => setOpenArchiveMap(false)}
        />
      )}
      {openDeleteMap && (
        <DeleteStoryMapForm
          storyMap={{ id: map.id, key: map.key, name: map.name }}
          onFormComplete={() => {
            deletedHereRef.current = true
            setOpenDeleteMap(false)
            // The map no longer exists — return to the list.
            router.push('/planning/story-maps')
          }}
          onFormCancel={() => setOpenDeleteMap(false)}
        />
      )}
    </div>
  )
}

const StoryMapDetailPageWithAuthorization = requireFeatureFlag(
  authorizePage(StoryMapDetailPage, 'Permission', 'Permissions.StoryMaps.View'),
  'story-maps',
)

export default StoryMapDetailPageWithAuthorization
