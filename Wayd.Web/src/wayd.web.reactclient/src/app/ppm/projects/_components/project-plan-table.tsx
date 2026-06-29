'use client'

import { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import dayjs from 'dayjs'
import { findOwnChildrenSpan, getChildrenContainmentError, isShiftOnlyChange } from './project-parent-date-hint'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  type DraftItem,
  type FilterOption,
  type MoveValidator,
  type TreeGridHandle,
  defaultMoveValidator,
  findNodeById,
  TreeGrid,
} from '@/src/components/common/tree-grid'
import CreateProjectTaskForm from './create-project-task-form'
import DeleteProjectTaskForm from './delete-project-task-form'
import EditProjectPhaseForm from './edit-project-phase-form'
import EditProjectTaskForm from './edit-project-task-form'
import ProjectPlanItemDrawer from './project-plan-item-drawer'
import {
  buildProjectPhasePatchOperations,
  buildProjectTaskPatchOperations,
} from './project-task-patch'
import { getProjectPlanTableColumns } from './project-plan-table.columns'
import { ProjectPlanHelp } from './project-plan-table.keyboard-shortcuts'
import {
  useGetTaskStatusOptionsQuery,
  useGetTaskPriorityOptionsQuery,
  useGetTaskTypeOptionsQuery,
  usePatchProjectTaskMutation,
  useUpdateProjectTaskPlacementMutation,
  useCreateProjectTaskMutation,
} from '@/src/store/features/ppm/project-tasks-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { usePatchProjectPhaseMutation } from '@/src/store/features/ppm/projects-api'
import { Form } from 'antd'

interface ProjectPlanTableProps {
  projectId: string
  projectKey: string
  tasks: ProjectPlanNodeDto[]
  isLoading: boolean
  canManageTasks: boolean
  refetch: () => Promise<any>
  enableDragAndDrop?: boolean
}

const ProjectPlanTable = ({
  projectId,
  projectKey,
  tasks = [],
  isLoading,
  canManageTasks,
  refetch,
  enableDragAndDrop = true,
}: ProjectPlanTableProps) => {
  const [form] = Form.useForm()
  const treeGridRef = useRef<TreeGridHandle>(null)
  const messageApi = useMessage()

  // Modal form state
  const [openCreateTaskForm, setOpenCreateTaskForm] = useState(false)
  const [openEditTaskForm, setOpenEditTaskForm] = useState(false)
  const [openDeleteTaskForm, setOpenDeleteTaskForm] = useState(false)
  const [openEditPhaseForm, setOpenEditPhaseForm] = useState(false)
  const [openPlanItemDrawer, setOpenPlanItemDrawer] = useState(false)
  const [selectedTaskId, setSelectedTaskId] = useState<string | undefined>()
  const [createParentTaskId, setCreateParentTaskId] = useState<string | undefined>()
  const [selectedPhaseId, setSelectedPhaseId] = useState<string | undefined>()
  const [selectedPlanItemId, setSelectedPlanItemId] = useState<string | null>(null)

  // Field errors (owned here for Form.useWatch access)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  // Keep a ref to drafts for use in handleUpdateTask
  const draftsRef = useRef<DraftItem[]>([])

  const { data: taskStatusOptions = [] } = useGetTaskStatusOptionsQuery()
  const { data: taskStatusOptionsForMilestone = [] } =
    useGetTaskStatusOptionsQuery({ forMilestone: true })
  const { data: taskPriorityOptions = [] } = useGetTaskPriorityOptionsQuery()
  const { data: taskTypeOptions = [] } = useGetTaskTypeOptionsQuery()
  const { data: employeeOptions = [] } = useGetEmployeeOptionsQuery(false)

  const milestoneTypeValue = useMemo(
    () => taskTypeOptions.find((opt) => opt.label === 'Milestone')?.value,
    [taskTypeOptions],
  )

  const [patchProjectTask] = usePatchProjectTaskMutation()
  const [patchProjectPhase] = usePatchProjectPhaseMutation()
  const [updateProjectTaskPlacement] = useUpdateProjectTaskPlacementMutation()
  const [createProjectTask] = useCreateProjectTaskMutation()

  // Track the selected row's type to dynamically show/hide milestone-specific fields
  const selectedTypeId = Form.useWatch('typeId', form)
  const isSelectedRowMilestone = selectedTypeId === milestoneTypeValue

  // Reset status to "Not Started" if user switches to Milestone while "In Progress" is selected
  useEffect(() => {
    if (isSelectedRowMilestone) {
      const currentStatus = form.getFieldValue('statusId')
      const inProgressValue = taskStatusOptions.find(
        (opt) => opt.label === 'In Progress',
      )?.value
      if (inProgressValue !== undefined && currentStatus === inProgressValue) {
        const notStartedValue = taskStatusOptions.find(
          (opt) => opt.label === 'Not Started',
        )?.value
        if (notStartedValue !== undefined) {
          form.setFieldValue('statusId', notStartedValue)
        }
      }
    }
  }, [isSelectedRowMilestone, form, taskStatusOptions])



  const taskTypeFilterOptions = useMemo(
    () => {
      const options = taskTypeOptions
        .map((o: any) => {
          const label = (o?.label ?? '') as string
          return label ? { label, value: label } : null
        })
        .filter((x): x is FilterOption => x !== null)

      if (!options.some((o) => o.value === 'Phase')) {
        options.unshift({ label: 'Phase', value: 'Phase' })
      }

      return options
    },
    [taskTypeOptions],
  )

  const taskStatusFilterOptions = useMemo(
    () =>
      taskStatusOptions
        .map((o: any) => {
          const label = (o?.label ?? '') as string
          return label ? { label, value: label } : null
        })
        .filter((x): x is FilterOption => x !== null),
    [taskStatusOptions],
  )

  const taskPriorityFilterOptions = useMemo(
    () =>
      taskPriorityOptions
        .map((o: any) => {
          const label = (o?.label ?? '') as string
          return label ? { label, value: label } : null
        })
        .filter((x): x is FilterOption => x !== null),
    [taskPriorityOptions],
  )

  const createDraftProjectTask = useCallback(
    (draft: DraftItem): ProjectPlanNodeDto => ({
      id: draft.id,
      nodeType: 'Task',
      key: '',
      wbs: '',
      name: '',
      type: { id: 1, name: 'Task' },
      status: { id: 1, name: 'Not Started' },
      priority: { id: 2, name: 'Medium' },
      assignees: [],
      progress: 0,
      order: draft.order,
      children: [],
    }),
    [],
  )

  const isPhaseNode = useCallback(
    (node: ProjectPlanNodeDto | null | undefined) => node?.nodeType === 'Phase',
    [],
  )

  const projectTaskMoveValidator: MoveValidator<ProjectPlanNodeDto> =
    useCallback(
      (activeNode, targetParentNode, targetParentId) => {
        // Phases cannot be moved
        if (isPhaseNode(activeNode.node)) {
          return { canMove: false, reason: 'Phases cannot be moved' }
        }
        const result = defaultMoveValidator(
          activeNode,
          targetParentNode,
          targetParentId,
        )
        if (!result.canMove) return result
        if (targetParentNode?.type?.name === 'Milestone') {
          return {
            canMove: false,
            reason: 'Milestones cannot have child tasks',
          }
        }
        return { canMove: true }
      },
      [isPhaseNode],
    )

  const handleNodeMove = useCallback(
    async (
      nodeId: string,
      parentId: string | null,
      order: number,
      overNodeId?: string,
      overIndex?: number,
    ) => {
      let resolvedParentId = parentId

      // When parentId is null (task dropped at root level), resolve to
      // the nearest phase above the drop position in the flat tree.
      if (resolvedParentId === null && overNodeId) {
        const flatten = (nodes: ProjectPlanNodeDto[]): ProjectPlanNodeDto[] => {
          const result: ProjectPlanNodeDto[] = []
          for (const node of nodes) {
            result.push(node)
            if (node.children?.length) {
              result.push(...flatten(node.children))
            }
          }
          return result
        }
        const flat = flatten(tasks)
        const activeIdx = flat.findIndex((n) => n.id === nodeId)
        const overIdx = overIndex ?? flat.findIndex((n) => n.id === overNodeId)
        const isDraggingUp = activeIdx > overIdx

        // Scan backwards from over index to find the nearest phase
        let nearestPhaseId: string | null = null
        for (let i = Math.min(overIdx, flat.length - 1); i >= 0; i--) {
          if (flat[i].nodeType === 'Phase') {
            nearestPhaseId = flat[i].id
            break
          }
        }

        if (nearestPhaseId) {
          // If dragging up and the nearest phase is the task's current phase,
          // use the phase above it instead (the user intends to leave this phase)
          const activeNode = flat[activeIdx]
          if (isDraggingUp && activeNode?.projectPhaseId === nearestPhaseId) {
            const phases = flat.filter((n) => n.nodeType === 'Phase')
            const phaseIdx = phases.findIndex((p) => p.id === nearestPhaseId)
            if (phaseIdx > 0) {
              resolvedParentId = phases[phaseIdx - 1].id
            } else {
              resolvedParentId = nearestPhaseId
            }
          } else {
            resolvedParentId = nearestPhaseId
          }
        }
      }

      if (!resolvedParentId) {
        messageApi.warning('Tasks must be placed within a phase')
        return
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
        await refetch()
      } catch (error: any) {
        messageApi.error(
          error?.data?.detail || 'Failed to move task. Please try again.',
        )
      }
    },
    [projectKey, tasks, updateProjectTaskPlacement, refetch, messageApi],
  )

  const handleTaskError = useCallback(
    (error: any, taskId: string, fallbackMessage: string): false => {
      const status = error?.status ?? error?.data?.status
      const errors = error?.errors ?? error?.data?.errors
      const detail = error?.detail ?? error?.data?.detail

      if (status === 422 && errors) {
        const errorMap: Record<string, string> = {}
        const errorFields: string[] = []
        Object.entries(errors).forEach(([key, messages]) => {
          const fieldName = key.charAt(0).toLowerCase() + key.slice(1)
          errorMap[fieldName] = Array.isArray(messages) ? messages[0] : messages
          errorFields.push(fieldName)
        })
        setFieldErrors(errorMap)

        setTimeout(() => {
          let focused = false
          for (const errorField of errorFields) {
            const columnId =
              errorField === 'plannedDate'
                ? 'plannedStart'
                : errorField.replace(/Id$/, '')
            const cellElement = document.querySelector(
              `[data-cell-id="${taskId}-${columnId}"]`,
            )
            if (cellElement) {
              const input = cellElement.querySelector(
                'input, .ant-select',
              ) as HTMLElement
              if (input) {
                input.focus({ preventScroll: true })
                focused = true
                break
              }
            }
          }
          if (!focused) {
            const cellElement = document.querySelector(
              `[data-cell-id="${taskId}-name"]`,
            )
            if (cellElement) {
              const input = cellElement.querySelector('input') as HTMLElement
              input?.focus({ preventScroll: true })
            }
          }
        }, 0)

        messageApi.error('Correct the validation error(s) to continue.')
      } else if (status === 400 && detail) {
        const detailStr = String(detail)
        const isDateError =
          detailStr.toLowerCase().includes('date') ||
          detailStr.toLowerCase().includes('child') ||
          detailStr.toLowerCase().includes('range')
        if (isDateError) {
          const isStartSpecific =
            detailStr.toLowerCase().includes('start') ||
            detailStr.toLowerCase().includes('before')
          const isEndSpecific =
            detailStr.toLowerCase().includes('end') ||
            detailStr.toLowerCase().includes('after')

          if (isStartSpecific && !isEndSpecific) {
            setFieldErrors({
              plannedStart: detailStr,
            })
          } else if (isEndSpecific && !isStartSpecific) {
            setFieldErrors({
              plannedEnd: detailStr,
            })
          } else {
            // General range/date error or clearing/null error applies to both
            setFieldErrors({
              plannedStart: detailStr,
              plannedEnd: detailStr,
            })
          }
          messageApi.error('Correct the validation error(s) to continue.')
        } else {
          messageApi.error(detailStr)
        }
      } else {
        messageApi.error(detail ?? fallbackMessage)
      }
      return false
    },
    [messageApi, setFieldErrors],
  )

  const handleUpdateTask = useCallback(
    async (taskId: string, updates: Partial<any>): Promise<boolean> => {
      if (!projectKey) return false

      const isDraft = taskId.startsWith('draft-')

      if (isDraft) {
        try {
          const draft = draftsRef.current.find((d) => d.id === taskId)
          if (!draft) return false

          const request: any = {
            name: updates.name || '',
            typeId: updates.typeId || 1,
            statusId: updates.statusId || 1,
            priorityId: updates.priorityId || 2,
            assigneeIds: updates.assigneeIds || [],
            progress: updates.progress,
            parentId: draft.parentId,
            plannedStart: updates.plannedStart,
            plannedEnd: updates.plannedEnd,
            plannedDate: updates.plannedDate,
            estimatedEffortHours: updates.estimatedEffortHours,
          }

          const response = await createProjectTask({
            projectIdOrKey: projectKey,
            request,
          })

          if (response.error) throw response.error

          messageApi.success(`Task created: ${response.data!.key}`)
          await refetch()
          return true
        } catch (error: any) {
          return handleTaskError(
            error,
            taskId,
            'An error occurred while creating the project task. Please try again.',
          )
        }
      } else {
        const node = findNodeById(
          tasks || [],
          taskId,
        ) as ProjectPlanNodeDto | null
        if (!node) return false

        try {
          if (isPhaseNode(node)) {
            const patchOperations = buildProjectPhasePatchOperations(updates)
            const response = await patchProjectPhase({
              projectId,
              projectKey,
              phaseId: taskId,
              patchOperations,
            })
            if (response.error) throw response.error
          } else {
            const patchOperations = buildProjectTaskPatchOperations(updates)
            const response = await patchProjectTask({
              projectIdOrKey: projectKey,
              taskId,
              patchOperations,
              cacheKey: taskId,
            })
            if (response.error) throw response.error
          }
          await refetch()
          return true
        } catch (error: any) {
          const entityName = isPhaseNode(node) ? 'phase' : 'task'
          return handleTaskError(
            error,
            taskId,
            `An error occurred while updating the project ${entityName}. Please try again.`,
          )
        }
      }
    },
    [
      messageApi,
      projectId,
      projectKey,
      refetch,
      tasks,
      patchProjectTask,
      patchProjectPhase,
      createProjectTask,
      handleTaskError,
      isPhaseNode,
    ],
  )

  const handleEditTask = useCallback((task: any) => {
    setSelectedTaskId(task.id)
    setOpenEditTaskForm(true)
  }, [])

  const handleEditPhase = useCallback((phase: any) => {
    setSelectedPhaseId(phase.id)
    setOpenEditPhaseForm(true)
  }, [])

  const handleDeleteTask = useCallback((task: any) => {
    setSelectedTaskId(task.id)
    setOpenDeleteTaskForm(true)
  }, [])

  const handleOpenPlanItemDrawer = useCallback((taskId: string) => {
    setSelectedPlanItemId(taskId)
    setOpenPlanItemDrawer(true)
  }, [])

  const onEditTaskFormClosed = useCallback(
    (wasSaved: boolean) => {
      setOpenEditTaskForm(false)
      setSelectedTaskId(undefined)
      if (wasSaved) refetch()
    },
    [refetch],
  )

  const onDeleteTaskFormClosed = useCallback(
    (wasDeleted: boolean) => {
      setOpenDeleteTaskForm(false)
      setSelectedTaskId(undefined)
      if (wasDeleted) refetch()
    },
    [refetch],
  )

  const onCreateTaskFormClosed = useCallback(
    (wasSaved: boolean) => {
      setOpenCreateTaskForm(false)
      setCreateParentTaskId(undefined)
      if (wasSaved) refetch()
    },
    [refetch],
  )

  const getFormValues = useCallback(
    (rowId: string, data: ProjectPlanNodeDto[]) => {
      const task = findNodeById(data, rowId) as ProjectPlanNodeDto | null
      const isDraft = rowId.startsWith('draft-')

      if (isDraft || !task) {
        return {
          name: '',
          typeId: 1,
          statusId: 1,
          priorityId: 2,
          assigneeIds: [],
          progress: 0,
          plannedStart: null,
          plannedEnd: null,
          plannedDate: null,
          estimatedEffortHours: null,
        }
      }

      // Phase rows: limited fields
      if (isPhaseNode(task)) {
        return {
          statusId: task.status?.id,
          assigneeIds: task.assignees?.map((a) => a.id) ?? [],
          plannedStart: task.start ? dayjs(task.start) : null,
          plannedEnd: task.end ? dayjs(task.end) : null,
          progress: task.progress,
        }
      }

      return {
        name: task.name,
        typeId: task.type?.id,
        statusId: task.status?.id,
        priorityId: task.priority?.id,
        assigneeIds: task.assignees?.map((a) => a.id) ?? [],
        progress: task.progress,
        plannedStart: task.start ? dayjs(task.start) : null,
        plannedEnd: task.end ? dayjs(task.end) : null,
        plannedDate: task.plannedDate ? dayjs(task.plannedDate) : null,
        estimatedEffortHours: task.estimatedEffortHours,
      }
    },
    [isPhaseNode],
  )

  const computeChanges = useCallback(
    (
      rowId: string,
      formValues: Record<string, any>,
      data: ProjectPlanNodeDto[],
    ) => {
      const isDraft = rowId.startsWith('draft-')
      const values = formValues as any
      const completedStatusValue = taskStatusOptions.find(
        (opt) => opt.label === 'Completed',
      )?.value
      const isCompleted = values.statusId === completedStatusValue

      if (isDraft) {
        const updates: Record<string, any> = {
          name: values.name || '',
          typeId: values.typeId,
          statusId: values.statusId,
          priorityId: values.priorityId,
          assigneeIds: values.assigneeIds ?? [],
        }
        if (isSelectedRowMilestone) {
          updates.plannedStart = null
          updates.plannedEnd = null
          updates.plannedDate = values.plannedDate
            ? values.plannedDate.format('YYYY-MM-DD')
            : null
          updates.progress = null
          updates.estimatedEffortHours = null
        } else {
          updates.plannedStart = values.plannedStart
            ? values.plannedStart.format('YYYY-MM-DD')
            : null
          updates.plannedEnd = values.plannedEnd
            ? values.plannedEnd.format('YYYY-MM-DD')
            : null
          updates.plannedDate = null
          updates.progress = isCompleted ? 100 : (values.progress ?? 0)
          updates.estimatedEffortHours = values.estimatedEffortHours
            ? Number(values.estimatedEffortHours)
            : null
        }
        return updates
      }

      const task = findNodeById(data, rowId) as ProjectPlanNodeDto | null
      if (!task) return null

      const updates: Record<string, any> = {}
      let hasChanges = false

      if (values.name !== task.name) {
        updates.name = values.name
        hasChanges = true
      }
      if (values.statusId !== task.status?.id) {
        updates.statusId = values.statusId
        hasChanges = true
      }
      if (values.priorityId !== task.priority?.id) {
        updates.priorityId = values.priorityId
        hasChanges = true
      }

      const taskAssigneeIds = task.assignees?.map((a) => a.id) ?? []
      const formAssigneeIds = values.assigneeIds ?? []
      const assigneesChanged =
        taskAssigneeIds.length !== formAssigneeIds.length ||
        !taskAssigneeIds.every((id: string) => formAssigneeIds.includes(id))
      if (assigneesChanged) {
        updates.assigneeIds = formAssigneeIds
        hasChanges = true
      }

      const taskPlannedStart = task.start
        ? String(task.start).split('T')[0]
        : null
      const plannedStartFormatted = values.plannedStart
        ? values.plannedStart.format('YYYY-MM-DD')
        : null
      if (plannedStartFormatted !== taskPlannedStart) {
        updates.plannedStart = plannedStartFormatted
        hasChanges = true
      }

      const taskPlannedEnd = task.end ? String(task.end).split('T')[0] : null
      const plannedEndFormatted = values.plannedEnd
        ? values.plannedEnd.format('YYYY-MM-DD')
        : null
      if (plannedEndFormatted !== taskPlannedEnd) {
        updates.plannedEnd = plannedEndFormatted
        hasChanges = true
      }

      const taskPlannedDate = task.plannedDate
        ? String(task.plannedDate).split('T')[0]
        : null
      const plannedDateFormatted = values.plannedDate
        ? values.plannedDate.format('YYYY-MM-DD')
        : null
      if (plannedDateFormatted !== taskPlannedDate) {
        updates.plannedDate = plannedDateFormatted
        hasChanges = true
      }

      const effectiveProgress = isCompleted ? 100 : values.progress
      if (effectiveProgress !== task.progress) {
        updates.progress = effectiveProgress
        hasChanges = true
      }

      if (values.estimatedEffortHours !== task.estimatedEffortHours) {
        updates.estimatedEffortHours = values.estimatedEffortHours
          ? Number(values.estimatedEffortHours)
          : null
        hasChanges = true
      }

      return hasChanges ? updates : null
    },
    [isSelectedRowMilestone, taskStatusOptions],
  )

  const validateFields = useCallback(
    (rowId: string, formValues: Record<string, any>) => {
      const isDraft = rowId.startsWith('draft-')
      const task = findNodeById(tasks, rowId) as ProjectPlanNodeDto | null
      const isMilestone = isDraft
        ? isSelectedRowMilestone
        : task?.type?.name === 'Milestone'

      if (isMilestone) return {}

      const errors: Record<string, string> = {}
      const hasPlannedStart = Boolean(formValues.plannedStart)
      const hasPlannedEnd = Boolean(formValues.plannedEnd)

      if (hasPlannedStart !== hasPlannedEnd) {
        const message =
          'Planned Start and Planned End must both have a value or both be empty.'
        errors.plannedStart = message
        errors.plannedEnd = message
      }

      if (hasPlannedStart && hasPlannedEnd) {
        const plannedStart = dayjs(formValues.plannedStart)
        const plannedEnd = dayjs(formValues.plannedEnd)
        if (plannedEnd.isBefore(plannedStart, 'day')) {
          errors.plannedEnd =
            'Planned End cannot be earlier than Planned Start.'
        }
      }

      // Check for date shrinking/clearing when children exist
      if (!isDraft && task) {
        const childrenSpan = findOwnChildrenSpan(tasks, rowId)
        if (childrenSpan) {
          if (!hasPlannedStart || !hasPlannedEnd) {
            const message = 'Planned dates cannot be cleared when child items have dates.'
            errors.plannedStart = message
            errors.plannedEnd = message
          } else {
            const start = dayjs(formValues.plannedStart)
            const end = dayjs(formValues.plannedEnd)
            const originalStart = task.start ? dayjs(task.start) : null
            const originalEnd = task.end ? dayjs(task.end) : null

            const isShift = isShiftOnlyChange(originalStart, originalEnd, start, end)

            if (!isShift) {
              const containmentError = getChildrenContainmentError(childrenSpan, start, end)
              if (containmentError) {
                if (containmentError.startsWith('Start')) {
                  errors.plannedStart = containmentError
                } else {
                  errors.plannedEnd = containmentError
                }
              }
            }
          }
        }
      }

      return errors
    },
    [isSelectedRowMilestone, tasks],
  )

  const selectedPlanItemPhaseName = useMemo(() => {
    if (!selectedPlanItemId) return undefined

    const selectedNode = findNodeById(
      tasks,
      selectedPlanItemId,
    ) as ProjectPlanNodeDto | null
    if (!selectedNode) return undefined

    const phaseId = selectedNode.projectPhaseId
    if (!phaseId) return undefined

    const phaseNode = findNodeById(tasks, phaseId) as ProjectPlanNodeDto | null
    return phaseNode?.name
  }, [selectedPlanItemId, tasks])

  return (
    <>
      <Form form={form} component={false}>
        <TreeGrid<ProjectPlanNodeDto>
          ref={treeGridRef}
          data={tasks}
          isLoading={isLoading}
          columns={(ctx) =>
            getProjectPlanTableColumns({
              canManageTasks,
              selectedRowId: ctx.selectedRowId,
              handleEditTask,
              handleDeleteTask,
              handleUpdateTask,
              getFieldError: ctx.getFieldError,
              handleKeyDown: ctx.handleKeyDown,
              createSelectInputKeyDown: ctx.createSelectInputKeyDown,
              taskStatusOptions,
              taskStatusOptionsForMilestone,
              taskPriorityOptions,
              taskTypeOptions,
              employeeOptions,
              isDragEnabled: ctx.isDragEnabled,
              enableDragAndDrop,
              addDraftTaskAsChild: ctx.addDraftAsChild,
              canCreateTasks: ctx.canCreateDraft,
              isSelectedRowMilestone,
              taskTypeFilterOptions,
              taskStatusFilterOptions,
              taskPriorityFilterOptions,
              isPhaseNode,
              handleEditPhase,
              openPlanItemDrawer: handleOpenPlanItemDrawer,
            })
          }
          onRefresh={refetch}
          enableDragAndDrop={enableDragAndDrop}
          onNodeMove={handleNodeMove}
          onMoveRejected={(reason) =>
            messageApi.warning(reason || 'Cannot move task to this location')
          }
          moveValidator={projectTaskMoveValidator}
          editingConfig={{
            canEdit: canManageTasks,
            form,
            editableColumnIds: (rowId) => {
              if (!rowId) {
                // Default columns used before any row is selected
                return [
                  'status',
                  'plannedStart',
                  'plannedEnd',
                  'assignees',
                  'name',
                  'priority',
                  'progress',
                  'estimatedEffortHours',
                ]
              }
              // Phase rows: limited editable fields
              const node = findNodeById(
                tasks,
                rowId,
              ) as ProjectPlanNodeDto | null
              if (node && isPhaseNode(node)) {
                return ['status', 'plannedStart', 'plannedEnd', 'assignees', 'progress']
              }
              const base = [
                'name',
                'status',
                'priority',
                'plannedStart',
                'plannedEnd',
                'assignees',
                'progress',
                'estimatedEffortHours',
              ]
              return rowId.startsWith('draft-')
                ? ['name', 'type', ...base.slice(1)]
                : base
            },
            onSave: handleUpdateTask,
            getFormValues,
            computeChanges,
            validateFields,
            cellIdColumnMatchOrder: [
              'estimatedEffortHours',
              'plannedStart',
              'plannedEnd',
              'assignees',
              'priority',
              'status',
              'progress',
              'type',
              'name',
            ],
          }}
          fieldErrors={fieldErrors}
          onFieldErrorsChange={setFieldErrors}
          createDraftNode={createDraftProjectTask}
          onDraftsChange={(drafts) => {
            draftsRef.current = drafts
          }}
          csvFileName="project-tasks"
          leftSlot={() => null}
          helpContent={<ProjectPlanHelp />}
          emptyMessage="No plan found"
        />
      </Form>

      {openCreateTaskForm && (
        <CreateProjectTaskForm
          projectIdOrKey={projectKey}
          parentTaskId={createParentTaskId}
          onFormComplete={() => onCreateTaskFormClosed(true)}
          onFormCancel={() => onCreateTaskFormClosed(false)}
        />
      )}
      {openEditTaskForm && selectedTaskId && (
        <EditProjectTaskForm
          projectIdOrKey={projectKey}
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
      {openEditPhaseForm && selectedPhaseId && (
        <EditProjectPhaseForm
          projectId={projectId}
          phaseId={selectedPhaseId}
          onFormComplete={() => {
            setOpenEditPhaseForm(false)
            setSelectedPhaseId(undefined)
            refetch()
          }}
          onFormCancel={() => {
            setOpenEditPhaseForm(false)
            setSelectedPhaseId(undefined)
          }}
        />
      )}
      <ProjectPlanItemDrawer
        projectKey={projectKey}
        taskId={selectedPlanItemId}
        phaseName={selectedPlanItemPhaseName}
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

export default ProjectPlanTable
