'use client'

import { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import { useRef, useState } from 'react'
import { Descriptions, Typography } from 'antd'
import dayjs from 'dayjs'
import { toCalendarDate } from '@/src/utils/calendar-date'
import { toApiError } from '@/src/utils/problem-details'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  type DraftItem,
  type FilterOption,
  type GridColumnContext,
  type MoveValidator,
  type WaydGridHandle,
  defaultMoveValidator,
  findNodeById,
  WaydGrid,
} from '@/src/components/common/wayd-grid'
import CreateProjectTaskForm from './create-project-task-form'
import DeleteProjectTaskForm from './delete-project-task-form'
import EditProjectStageForm from './edit-project-stage-form'
import EditProjectTaskForm from './edit-project-task-form'
import ProjectPlanItemDrawer from './project-plan-item-drawer'
import { getProjectPlanTableColumns } from './project-plan-table.columns'
import {
  DRAFT_TASK_DEFAULTS,
  PLAN_FIELD_TO_COLUMN,
  isMilestoneRow,
  isStageRow,
  planAssigneesEditor,
  planPriorityEditor,
  planStatusEditor,
  planTypeEditor,
  toCreateProjectTaskRequest,
  asMilestoneDate,
  toProjectPlanPatch,
  withCompletionProgress,
} from './project-plan-table.editors'
import {
  useProjectPlanGantt,
  computeProjectPlanGanttDomain,
  pxPerMsFor,
} from './project-plan-gantt'
import {
  GanttToolbarActions,
  useBarDrag,
  useGanttVisibility,
  useGanttZoom,
  useChartPaneWidth,
  type GanttDragItem,
} from '@/src/components/common/timeline'
import {
  useGetTaskStatusOptionsQuery,
  useGetTaskPriorityOptionsQuery,
  useGetTaskTypeOptionsQuery,
  usePatchProjectTaskMutation,
  useUpdateProjectTaskPlacementMutation,
  useCreateProjectTaskMutation,
} from '@/src/store/features/ppm/project-tasks-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useGetProjectDeliverablesQuery } from '@/src/store/features/ppm/project-deliverables-api'
import { usePatchProjectStageMutation } from '@/src/store/features/ppm/projects-api'

interface ProjectPlanTableProps {
  projectId: string
  projectKey: string
  tasks: ProjectPlanNodeDto[]
  isLoading: boolean
  /** The user may change the plan: the Update permission and delivery
   *  leadership on the project. */
  canManageTasks: boolean
  refetch: () => Promise<any>
  enableDragAndDrop?: boolean
  /** The toolbar's left slot, where the plan's Group by choice sits. */
  leftSlot?: React.ReactNode
}

/** Option labels as set-filter choices. */
const toFilterOptions = (options: { label?: unknown }[]): FilterOption[] =>
  options
    .map((option) => String(option?.label ?? ''))
    .filter(Boolean)
    .map((label) => ({ label, value: label }))

const createDraftProjectTask = (
  draft: DraftItem,
  type: { typeId: number; typeName: string } = DRAFT_TASK_DEFAULTS,
): ProjectPlanNodeDto => ({
  id: draft.id,
  nodeType: 'Task',
  key: '',
  wbs: '',
  name: '',
  type: { id: type.typeId, name: type.typeName },
  status: {
    id: DRAFT_TASK_DEFAULTS.statusId,
    name: DRAFT_TASK_DEFAULTS.statusName,
  },
  priority: {
    id: DRAFT_TASK_DEFAULTS.priorityId,
    name: DRAFT_TASK_DEFAULTS.priorityName,
  },
  assignees: [],
  progress: DRAFT_TASK_DEFAULTS.progress,
  order: draft.order,
  children: [],
  deliverableIds: [],
})

/** A new task goes under a stage or a task, never at the root or under a
 *  milestone. */
const canHoldTasks = (parent: ProjectPlanNodeDto | null) =>
  !!parent && !isMilestoneRow(parent)

const projectTaskMoveValidator: MoveValidator<ProjectPlanNodeDto> = (
  activeNode,
  targetParentNode,
  targetParentId,
) => {
  if (isStageRow(activeNode.node)) {
    return { canMove: false, reason: 'Stages cannot be moved' }
  }
  const result = defaultMoveValidator(
    activeNode,
    targetParentNode,
    targetParentId,
  )
  if (!result.canMove) return result
  if (targetParentNode && isMilestoneRow(targetParentNode)) {
    return { canMove: false, reason: 'Milestones cannot have child tasks' }
  }
  return { canMove: true }
}

const flattenPlan = (nodes: ProjectPlanNodeDto[]): ProjectPlanNodeDto[] =>
  nodes.flatMap((node) => [node, ...flattenPlan(node.children ?? [])])

/** The row that takes a removed row's place: its next sibling, else the one
 *  before it, else its parent. */
const rowAfterRemoving = (
  nodes: ProjectPlanNodeDto[],
  id: string,
  parentId: string | null = null,
): string | null => {
  const index = nodes.findIndex((node) => node.id === id)
  if (index >= 0) {
    return nodes[index + 1]?.id ?? nodes[index - 1]?.id ?? parentId
  }
  for (const node of nodes) {
    const found = rowAfterRemoving(node.children ?? [], id, node.id)
    if (found) return found
  }
  return null
}

const ProjectPlanTable = ({
  projectId,
  projectKey,
  tasks = [],
  isLoading,
  canManageTasks,
  refetch,
  enableDragAndDrop = true,
  leftSlot,
}: ProjectPlanTableProps) => {
  const gridRef = useRef<WaydGridHandle>(null)
  // Tasks this grid created, by id, until the plan shows them.
  const justCreatedRef = useRef(new Map<string, ProjectPlanNodeDto>())
  const messageApi = useMessage()

  // Modal form state
  const [openCreateTaskForm, setOpenCreateTaskForm] = useState(false)
  const [openEditTaskForm, setOpenEditTaskForm] = useState(false)
  const [openDeleteTaskForm, setOpenDeleteTaskForm] = useState(false)
  const [openEditStageForm, setOpenEditStageForm] = useState(false)
  const [openPlanItemDrawer, setOpenPlanItemDrawer] = useState(false)
  const [selectedTaskId, setSelectedTaskId] = useState<string | undefined>()
  const [createParentTaskId, setCreateParentTaskId] = useState<
    string | undefined
  >()
  const [selectedStageId, setSelectedStageId] = useState<string | undefined>()
  const [selectedPlanItemId, setSelectedPlanItemId] = useState<string | null>(
    null,
  )

  const { data: taskStatusOptions = [] } = useGetTaskStatusOptionsQuery()
  const { data: taskStatusOptionsForMilestone = [] } =
    useGetTaskStatusOptionsQuery({ forMilestone: true })
  const { data: taskPriorityOptions = [] } = useGetTaskPriorityOptionsQuery()
  const { data: taskTypeOptions = [] } = useGetTaskTypeOptionsQuery()
  const { data: employeeOptions = [] } = useGetEmployeeOptionsQuery(false)
  const { data: deliverables = [] } = useGetProjectDeliverablesQuery(projectId)
  const deliverableNames = new Map(deliverables.map((d) => [d.id, d.name]))

  const milestoneTypeId = taskTypeOptions.find(
    (option) => option.label === 'Milestone',
  )?.value
  const completedStatusId = taskStatusOptions.find(
    (option) => option.label === 'Completed',
  )?.value

  const [patchProjectTask] = usePatchProjectTaskMutation()
  const [patchProjectStage] = usePatchProjectStageMutation()
  const [updateProjectTaskPlacement] = useUpdateProjectTaskPlacementMutation()
  const [createProjectTask] = useCreateProjectTaskMutation()

  const taskTypeFilterOptions = toFilterOptions(taskTypeOptions)
  if (!taskTypeFilterOptions.some((o) => o.value === 'Stage')) {
    taskTypeFilterOptions.unshift({ label: 'Stage', value: 'Stage' })
  }
  const taskStatusFilterOptions = toFilterOptions(taskStatusOptions)
  const taskPriorityFilterOptions = toFilterOptions(taskPriorityOptions)

  const optionEditors = {
    type: planTypeEditor(taskTypeOptions),
    status: planStatusEditor(taskStatusOptions, taskStatusOptionsForMilestone),
    priority: planPriorityEditor(taskPriorityOptions),
    assignees: planAssigneesEditor(
      employeeOptions.map((option) => ({
        label: String(option.label ?? ''),
        value: String(option.value),
      })),
    ),
  }

  const handleNodeMove = async (
    nodeId: string,
    parentId: string | null,
    order: number,
    overNodeId?: string,
    overIndex?: number,
  ) => {
    let resolvedParentId = parentId

    // When parentId is null (task dropped at root level), resolve to
    // the nearest stage above the drop position in the flat tree.
    if (resolvedParentId === null && overNodeId) {
      const flat = flattenPlan(tasks)
      const activeIdx = flat.findIndex((n) => n.id === nodeId)
      const overIdx = overIndex ?? flat.findIndex((n) => n.id === overNodeId)
      const isDraggingUp = activeIdx > overIdx

      // Scan backwards from over index to find the nearest stage
      let nearestStageId: string | null = null
      for (let i = Math.min(overIdx, flat.length - 1); i >= 0; i--) {
        if (flat[i].nodeType === 'Stage') {
          nearestStageId = flat[i].id
          break
        }
      }

      if (nearestStageId) {
        // If dragging up and the nearest stage is the task's current stage,
        // use the stage above it instead (the user intends to leave this stage)
        const activeNode = flat[activeIdx]
        if (isDraggingUp && activeNode?.projectStageId === nearestStageId) {
          const stages = flat.filter((n) => n.nodeType === 'Stage')
          const stageIdx = stages.findIndex((p) => p.id === nearestStageId)
          resolvedParentId =
            stageIdx > 0 ? stages[stageIdx - 1].id : nearestStageId
        } else {
          resolvedParentId = nearestStageId
        }
      }
    }

    // Failures reject, so the grid reports them (onMoveRejected) and a
    // keyboard move knows the row stayed where it was.
    if (!resolvedParentId) {
      throw new Error('Tasks must be placed within a stage')
    }

    try {
      await updateProjectTaskPlacement({
        projectIdOrKey: projectKey,
        id: nodeId,
        request: {
          taskId: nodeId,
          parentId: resolvedParentId,
          order,
        },
      }).unwrap()
    } catch (error) {
      throw new Error(
        toApiError(error).detail ||
          toApiError((error as { data?: unknown })?.data).detail ||
          'Failed to move task. Please try again.',
      )
    }
    await refetch()
  }

  /** Saves one edited cell (or a start/end pair) of a task or stage. */
  const commitPlanItem = async (
    rowId: string,
    patch: Record<string, unknown>,
  ) => {
    // A task created moments ago may not be in the plan yet: values typed
    // while it was created are sent before the refetch lands.
    const node =
      (findNodeById(tasks, rowId) as ProjectPlanNodeDto | null) ??
      justCreatedRef.current.get(rowId)
    if (!node) throw new Error('The item is no longer in the plan.')
    const patchOperations = toProjectPlanPatch(
      node,
      withCompletionProgress(
        node,
        asMilestoneDate(node, patch),
        completedStatusId,
      ),
    )
    if (isStageRow(node)) {
      await patchProjectStage({
        projectId,
        projectKey,
        stageId: rowId,
        patchOperations,
      }).unwrap()
    } else {
      await patchProjectTask({
        projectIdOrKey: projectKey,
        taskId: rowId,
        patchOperations,
        cacheKey: rowId,
      }).unwrap()
    }
  }

  /**
   * Creates a task from a draft once it has a name (and a milestone's date);
   * returns its id. A task added below a row is created in that place.
   */
  const createPlanTask = async (
    parentId: string | null,
    values: Record<string, unknown>,
    position?: { order: number },
  ) => {
    if (!parentId) throw new Error('Tasks must be placed within a stage.')
    const request = {
      ...toCreateProjectTaskRequest(
        parentId,
        values,
        milestoneTypeId,
        completedStatusId,
      ),
      ...(position ? { order: position.order } : {}),
    }
    const created = await createProjectTask({
      projectIdOrKey: projectKey,
      request,
    }).unwrap()
    justCreatedRef.current.set(
      created.id,
      createDraftProjectTask(
        { id: created.id, order: 0 },
        {
          typeId: request.typeId,
          typeName: request.typeId === milestoneTypeId ? 'Milestone' : 'Task',
        },
      ),
    )
    return created.id
  }

  // ── Gantt chart pane ──────────────────────────────────────────────────────
  // Stages, tasks and milestones drawn on a shared time axis to the right of the
  // grid. Chart mechanics live in the shared engine (components/common/timeline/
  // gantt); project-plan-gantt.tsx supplies the accessors for our node shape.
  // Show/hide the chart. Persisted across projects (see useGanttVisibility).
  const ganttVisibility = useGanttVisibility('project-plan')
  const showGantt = ganttVisibility.visible

  // A dragged bar's dates go through the grid like a cell edit: the cells
  // and the bar show them marked saving, the date rules check them first,
  // they wait behind the row's other commits, and a failure lands on the
  // Planned Start and End cells.
  const commitBarDates = (change: {
    id: string
    start: number
    end: number
  }) => {
    const node = findNodeById(tasks, change.id) as ProjectPlanNodeDto | null
    if (!node) return
    const start = toCalendarDate(dayjs(change.start))
    const end = toCalendarDate(dayjs(change.end))
    gridRef.current?.commitCells(
      change.id,
      isMilestoneRow(node)
        ? { plannedStart: start }
        : { plannedStart: start, plannedEnd: end },
    )
  }

  // Domain drives the drag clamp range; pxPerMs comes from the zoom level. Both
  // are computed the same way the chart's scale is (no duplication).
  const { domainStart, domainEnd } = computeProjectPlanGanttDomain(tasks)

  // Zoom is floored so the chart always fills the pane — zooming out past the
  // fit would leave the axis short of the right edge. Needs the domain and the
  // live pane width, so it follows both.
  const chartPaneWidth = useChartPaneWidth(showGantt)
  const zoom = useGanttZoom(undefined, {
    domain: [domainStart, domainEnd],
    viewportWidth: chartPaneWidth,
  })

  const barDrag = useBarDrag({
    pxPerMs: pxPerMsFor(zoom.pxPerDay),
    min: domainStart,
    max: domainEnd,
    onCommit: commitBarDates,
  })

  // Pointer offset from the bar's left edge at move-drag start, so the live date
  // label can follow the cursor rather than centering on the whole bar.
  const [grabOffset, setGrabOffset] = useState(0)
  const onBarPointerDown = (
    e: React.PointerEvent,
    item: GanttDragItem,
    mode: 'move' | 'resize-start' | 'resize-end',
  ) => {
    if (mode === 'move') setGrabOffset(e.nativeEvent.offsetX)
    barDrag.start(e, item, mode)
  }

  // Called unconditionally (rules of hooks); the pane is only wired when shown.
  const gantt = useProjectPlanGantt(tasks, {
    pxPerDay: zoom.pxPerDay,
    editable: canManageTasks,
    activeDrag: barDrag.active,
    onBarPointerDown,
    moveGrabOffset: barDrag.active?.mode === 'move' ? grabOffset : undefined,
  })

  const handleEditTask = (task: ProjectPlanNodeDto) => {
    setSelectedTaskId(task.id)
    setOpenEditTaskForm(true)
  }

  const handleEditStage = (stage: ProjectPlanNodeDto) => {
    setSelectedStageId(stage.id)
    setOpenEditStageForm(true)
  }

  const handleDeleteTask = (task: ProjectPlanNodeDto) => {
    setSelectedTaskId(task.id)
    setOpenDeleteTaskForm(true)
  }

  const handleOpenPlanItemDrawer = (taskId: string) => {
    setSelectedPlanItemId(taskId)
    setOpenPlanItemDrawer(true)
  }

  const onEditTaskFormClosed = (wasSaved: boolean) => {
    setOpenEditTaskForm(false)
    setSelectedTaskId(undefined)
    if (wasSaved) refetch()
  }

  const onDeleteTaskFormClosed = (wasDeleted: boolean) => {
    // The deleted row's cell held focus; it moves to the row taking its place.
    const next = selectedTaskId ? rowAfterRemoving(tasks, selectedTaskId) : null
    const deleting = selectedTaskId
    setOpenDeleteTaskForm(false)
    setSelectedTaskId(undefined)
    // The form unmounts without handing focus back, so a cancel returns it.
    if (!wasDeleted) {
      if (deleting) gridRef.current?.focusRow(deleting, 'name')
      return
    }
    void refetch().then(() => {
      if (next) gridRef.current?.focusRow(next, 'name')
    })
  }

  const onCreateTaskFormClosed = (wasSaved: boolean) => {
    setOpenCreateTaskForm(false)
    setCreateParentTaskId(undefined)
    if (wasSaved) refetch()
  }

  const selectedPlanItemStageName = (() => {
    if (!selectedPlanItemId) return undefined
    const selectedNode = findNodeById(
      tasks,
      selectedPlanItemId,
    ) as ProjectPlanNodeDto | null
    const stageId = selectedNode?.projectStageId
    if (!stageId) return undefined
    return (findNodeById(tasks, stageId) as ProjectPlanNodeDto | null)?.name
  })()

  const columns = (ctx: GridColumnContext) =>
    getProjectPlanTableColumns({
      canManageTasks,
      handleEditTask,
      handleDeleteTask,
      handleEditStage,
      openPlanItemDrawer: handleOpenPlanItemDrawer,
      isDragEnabled: ctx.isDragEnabled,
      enableDragAndDrop,
      addDraftTaskAsChild: ctx.addDraftAsChild,
      canCreateTasks: ctx.canCreateDraft,
      taskTypeFilterOptions,
      taskStatusFilterOptions,
      taskPriorityFilterOptions,
      optionEditors,
      deliverableNames,
    })

  return (
    <>
      <WaydGrid<ProjectPlanNodeDto>
        ref={gridRef}
        data={tasks}
        getSubRows={(row) => row.children as ProjectPlanNodeDto[]}
        isLoading={isLoading}
        columns={columns}
        actionsSlot={
          <GanttToolbarActions
            visible={showGantt}
            onToggle={ganttVisibility.toggle}
            zoom={zoom}
          />
        }
        rightPane={
          showGantt
            ? {
                header: gantt.header,
                defaultWidth: gantt.defaultWidth,
                renderRow: gantt.renderRow,
                renderBackground: gantt.renderBackground,
                // Ctrl/Cmd+wheel zooms the chart (matches the timeline).
                onWheel: zoom.onWheel,
              }
            : undefined
        }
        onRefresh={refetch}
        enableDragAndDrop={enableDragAndDrop}
        onNodeMove={canManageTasks ? handleNodeMove : undefined}
        onMoveRejected={(reason) =>
          messageApi.warning(reason || 'Cannot move task to this location')
        }
        moveValidator={projectTaskMoveValidator}
        onCommit={canManageTasks ? commitPlanItem : undefined}
        onCreate={canManageTasks ? createPlanTask : undefined}
        fieldToColumn={PLAN_FIELD_TO_COLUMN}
        createDraftNode={canManageTasks ? createDraftProjectTask : undefined}
        rowActions={{
          ...(canManageTasks
            ? {
                canInsertUnder: canHoldTasks,
                onDelete: (row: ProjectPlanNodeDto) => {
                  if (isStageRow(row)) return false
                  handleDeleteTask(row)
                },
              }
            : {}),
          onOpen: (row) => {
            if (isStageRow(row)) return false
            handleOpenPlanItemDrawer(row.id)
          },
        }}
        csvFileName="project-tasks"
        leftSlot={leftSlot ?? (() => null)}
        helpNotes={
          canManageTasks && enableDragAndDrop ? <DragAndDropHelp /> : undefined
        }
        emptyMessage="No plan found"
      />

      {openCreateTaskForm && (
        <CreateProjectTaskForm
          projectIdOrKey={projectKey}
          projectId={projectId}
          parentTaskId={createParentTaskId}
          onFormComplete={() => onCreateTaskFormClosed(true)}
          onFormCancel={() => onCreateTaskFormClosed(false)}
        />
      )}
      {openEditTaskForm && selectedTaskId && (
        <EditProjectTaskForm
          projectIdOrKey={projectKey}
          projectId={projectId}
          taskIdOrKey={selectedTaskId}
          onFormComplete={() => onEditTaskFormClosed(true)}
          onFormCancel={() => onEditTaskFormClosed(false)}
        />
      )}
      {openDeleteTaskForm && selectedTaskId && (
        <DeleteProjectTaskForm
          projectIdOrKey={projectKey}
          taskIdOrKey={selectedTaskId}
          onFormComplete={() => onDeleteTaskFormClosed(true)}
          onFormCancel={() => onDeleteTaskFormClosed(false)}
        />
      )}
      {openEditStageForm && selectedStageId && (
        <EditProjectStageForm
          projectId={projectId}
          projectKey={projectKey}
          stageId={selectedStageId}
          onFormComplete={() => {
            setOpenEditStageForm(false)
            setSelectedStageId(undefined)
            refetch()
          }}
          onFormCancel={() => {
            setOpenEditStageForm(false)
            setSelectedStageId(undefined)
          }}
        />
      )}
      <ProjectPlanItemDrawer
        projectKey={projectKey}
        taskId={selectedPlanItemId}
        stageName={selectedPlanItemStageName}
        drawerOpen={openPlanItemDrawer}
        onOpenTask={handleOpenPlanItemDrawer}
        onEditTask={(taskId) => {
          setOpenPlanItemDrawer(false)
          setSelectedPlanItemId(null)
          handleEditTask({ id: taskId } as ProjectPlanNodeDto)
        }}
        onDeleteTask={(taskId) => {
          setOpenPlanItemDrawer(false)
          setSelectedPlanItemId(null)
          handleDeleteTask({ id: taskId } as ProjectPlanNodeDto)
        }}
        onAddChildTask={(taskId) => {
          setOpenPlanItemDrawer(false)
          setSelectedPlanItemId(null)
          setCreateParentTaskId(taskId)
          setOpenCreateTaskForm(true)
        }}
        onDrawerClose={() => {
          setOpenPlanItemDrawer(false)
          setSelectedPlanItemId(null)
        }}
      />
    </>
  )
}

/** The plan's drag-and-drop gestures, listed after the keys. */
function DragAndDropHelp() {
  return (
    <Descriptions
      size="small"
      column={1}
      styles={{ header: { marginBottom: 4 } }}
      title="Drag and Drop"
      items={[
        ['Drag handle', 'Drag the grip icon to move a task'],
        ['Drag up or down', 'Move the task up or down the plan'],
        ['Drag right', 'Nest the task under the item above it'],
        ['Drag left', 'Move the task up a level'],
        ['Drag a bar', 'Move or resize its dates in the chart'],
      ].map(([label, text]) => ({
        key: label,
        label,
        children: <Typography.Text>{text}</Typography.Text>,
      }))}
    />
  )
}

export default ProjectPlanTable
