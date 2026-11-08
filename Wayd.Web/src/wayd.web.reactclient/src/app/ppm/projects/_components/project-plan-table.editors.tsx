'use client'

import {
  dateEditor,
  numberEditor,
  pairedEditors,
  selectEditor,
  textEditor,
  type SelectEditorOption,
} from '@/src/components/common/wayd-grid'
import type {
  CreateProjectTaskRequest,
  EmployeeNavigationDto,
  JsonPatchOperation,
  ProjectPlanNodeDto,
} from '@/src/services/wayd-api'
import { parseCalendarDate, type CalendarDate } from '@/src/utils/calendar-date'
import { rangeChangeContainmentError } from '@/src/utils/date-containment'
import { planChildrenSpan } from './project-parent-date-hint'
import {
  buildProjectStagePatchOperations,
  buildProjectTaskPatchOperations,
} from './project-task-patch'

type PlanRow = ProjectPlanNodeDto

export const isMilestoneRow = (row: PlanRow) => row.type?.name === 'Milestone'
export const isStageRow = (row: PlanRow) => row.nodeType === 'Stage'
const isDraftRow = (row: PlanRow) => row.id.startsWith('draft-')

/**
 * What a new task starts as, shown on its draft row and sent when the user
 * leaves a value as it is: a Task, Not Started, Medium priority, 0% done.
 * The ids are the API's enum values.
 */
export const DRAFT_TASK_DEFAULTS = {
  typeId: 1,
  typeName: 'Task',
  statusId: 1,
  statusName: 'Not Started',
  priorityId: 2,
  priorityName: 'Medium',
  progress: 0,
} as const

const labelOf = <V,>(options: SelectEditorOption<V>[], value: V) =>
  options.find((option) => option.value === value)?.label ?? ''

/** Edits a task's name; required, 128 characters at most. A stage's name
 *  comes from its lifecycle. */
export const planNameEditor = textEditor<PlanRow>({
  field: 'name',
  get: (row) => row.name,
  apply: (row, name) => ({ ...row, name }),
  maxLength: 128,
  validate: (name) => (name.trim() ? undefined : 'Name is required.'),
  editable: (row) => !isStageRow(row),
})

/** Chooses a new task's type. An existing task keeps the type it has. */
export const planTypeEditor = (options: SelectEditorOption<number>[]) =>
  selectEditor<PlanRow, number>({
    field: 'typeId',
    get: (row) => row.type?.id ?? DRAFT_TASK_DEFAULTS.typeId,
    apply: (row, id) => ({ ...row, type: { id, name: labelOf(options, id) } }),
    options,
    editable: (_row, { isDraft }) => isDraft,
  })

/** A task's or stage's status. A milestone is never In Progress, so it is
 *  offered only the statuses a milestone can have. */
export const planStatusEditor = (
  options: SelectEditorOption<number>[],
  milestoneOptions: SelectEditorOption<number>[],
) =>
  selectEditor<PlanRow, number>({
    field: 'statusId',
    get: (row) => row.status?.id,
    apply: (row, id) => ({
      ...row,
      status: { id, name: labelOf(options, id) },
    }),
    options: (row) => (isMilestoneRow(row) ? milestoneOptions : options),
    // A draft can become a milestone after its status was chosen.
    validate: (id, row) =>
      isMilestoneRow(row) &&
      milestoneOptions.length > 0 &&
      !milestoneOptions.some((option) => option.value === id)
        ? 'A milestone can only be Not Started or Completed.'
        : undefined,
  })

/**
 * The patch with what completing an item implies: a task or stage set to
 * Completed is 100% done. A milestone's progress is the server's to set.
 */
export const withCompletionProgress = (
  row: PlanRow,
  patch: Record<string, unknown>,
  completedStatusId: number | undefined,
): Record<string, unknown> =>
  completedStatusId !== undefined &&
  patch.statusId === completedStatusId &&
  !isMilestoneRow(row) &&
  patch.progress === undefined
    ? { ...patch, progress: 100 }
    : patch

/** A task's priority. Stages have none. */
export const planPriorityEditor = (options: SelectEditorOption<number>[]) =>
  selectEditor<PlanRow, number>({
    field: 'priorityId',
    get: (row) => row.priority?.id ?? DRAFT_TASK_DEFAULTS.priorityId,
    apply: (row, id) => ({
      ...row,
      priority: { id, name: labelOf(options, id) },
    }),
    options,
    editable: (row) => !isStageRow(row),
  })

/** The people a task or stage is assigned to. */
export const planAssigneesEditor = (employees: SelectEditorOption<string>[]) =>
  selectEditor<PlanRow, string[]>({
    field: 'assigneeIds',
    get: (row) => (row.assignees ?? []).map((assignee) => assignee.id),
    apply: (row, ids) => ({
      ...row,
      assignees: (ids ?? []).map(
        (id) =>
          (row.assignees?.find((assignee) => assignee.id === id) ?? {
            id,
            name: labelOf(employees, id),
          }) as EmployeeNavigationDto,
      ),
    }),
    options: employees,
    multiple: true,
  })

/** How complete a task or stage is, 0–100. A milestone has no progress. */
export const planProgressEditor = numberEditor<PlanRow>({
  field: 'progress',
  get: (row) => row.progress ?? null,
  apply: (row, progress) => ({ ...row, progress: progress ?? 0 }),
  min: 0,
  max: 100,
  suffix: '%',
  validate: (value) =>
    value === null
      ? 'Progress is required.'
      : value < 0 || value > 100
        ? 'Progress must be between 0 and 100.'
        : undefined,
  editable: (row) => !isMilestoneRow(row),
})

/** A task's estimated effort in hours; empty for no estimate. */
export const planEffortEditor = numberEditor<PlanRow>({
  field: 'estimatedEffortHours',
  get: (row) => row.estimatedEffortHours ?? null,
  apply: (row, hours) => ({
    ...row,
    estimatedEffortHours: hours ?? undefined,
  }),
  min: 0,
  suffix: 'h',
  validate: (value) =>
    value !== null && value <= 0
      ? 'Estimated effort must be greater than 0.'
      : undefined,
  editable: (row) => !isStageRow(row) && !isMilestoneRow(row),
})

const BOTH_OR_NEITHER =
  'Planned Start and Planned End must both have a value or both be empty.'
const CLEARED_WITH_CHILDREN =
  'Planned dates cannot be cleared when child items have dates.'

/**
 * A task's or stage's planned start and end, checked together: both or
 * neither, the end not before the start, and a parent's range still holding
 * its children unless the whole range moved (a shift carries them along). A
 * milestone's single date is edited in the Start column, and a milestone
 * draft is not created without it.
 */
export const [planStartEditor, planEndEditor] = pairedEditors<
  PlanRow,
  CalendarDate | null,
  CalendarDate | null
>({
  start: dateEditor<PlanRow>({
    // A draft keeps one key whatever its Type, since the Type can change
    // after the date is entered; its create request picks the field.
    field: (row) =>
      isMilestoneRow(row) && !isDraftRow(row) ? 'plannedDate' : 'plannedStart',
    get: (row) => (isMilestoneRow(row) ? row.plannedDate : row.start) ?? null,
    apply: (row, value) =>
      isDraftRow(row)
        ? { ...row, start: value ?? undefined, plannedDate: value ?? undefined }
        : isMilestoneRow(row)
          ? { ...row, plannedDate: value ?? undefined }
          : { ...row, start: value ?? undefined },
    validate: (value, row) =>
      !value && isMilestoneRow(row) ? 'A milestone needs a date.' : undefined,
    requiredToCreate: (row) => isMilestoneRow(row),
  }),
  end: dateEditor<PlanRow>({
    field: 'plannedEnd',
    get: (row) => row.end ?? null,
    apply: (row, value) => ({ ...row, end: value ?? undefined }),
    editable: (row) => !isMilestoneRow(row),
  }),
  validate: ({ start, end }, row) => {
    if (isMilestoneRow(row)) return undefined
    const span = planChildrenSpan(row.children)
    if (!start || !end) {
      if (!start && !end) {
        return span
          ? { start: CLEARED_WITH_CHILDREN, end: CLEARED_WITH_CHILDREN }
          : undefined
      }
      return { start: BOTH_OR_NEITHER, end: BOTH_OR_NEITHER }
    }
    if (end < start) {
      return { end: 'Planned End cannot be earlier than Planned Start.' }
    }
    const containment = rangeChangeContainmentError({
      original: [
        row.start ? parseCalendarDate(row.start) : null,
        row.end ? parseCalendarDate(row.end) : null,
      ],
      next: [parseCalendarDate(start), parseCalendarDate(end)],
      span,
    })
    return containment ? { [containment.role]: containment.message } : undefined
  },
})

/**
 * The patch with a milestone's date under its own key. A draft's date is
 * held as `plannedStart` whatever its Type, so a value typed while the draft
 * was being created reaches the new milestone under that key.
 */
export const asMilestoneDate = (
  row: PlanRow,
  patch: Record<string, unknown>,
): Record<string, unknown> => {
  if (!isMilestoneRow(row) || !('plannedStart' in patch)) return patch
  const { plannedStart, plannedEnd: _end, ...rest } = patch
  return { ...rest, plannedDate: plannedStart }
}

/**
 * Server error keys that name no editor's field: a milestone's date lives in
 * the Start column, and the stage endpoint calls its status `Status`.
 */
export const PLAN_FIELD_TO_COLUMN = {
  // Start's field differs by row, so its errors are not found by field.
  plannedStart: 'plannedStart',
  plannedDate: 'plannedStart',
  status: 'status',
}

/**
 * The JSON Patch for a commit on a task or stage, a path per changed field.
 * A field the endpoint has no path for throws, so the cell is marked failed
 * rather than the patch going out without it.
 */
export const toProjectPlanPatch = (
  row: PlanRow,
  patch: Record<string, unknown>,
): JsonPatchOperation[] => {
  const build = isStageRow(row)
    ? buildProjectStagePatchOperations
    : buildProjectTaskPatchOperations
  return Object.entries(patch).flatMap(([field, value]) => {
    const operations = build({ [field]: value } as never)
    if (operations.length === 0) {
      throw new Error(`The project plan cannot save ${field}.`)
    }
    return operations
  })
}

/**
 * The create request for a new task, from the values the user put on its
 * draft and the defaults the draft showed for the rest. A draft's Start is
 * a milestone's single date; a milestone carries no range, progress or
 * effort, and a task no single date. A task created Completed is 100% done.
 */
export const toCreateProjectTaskRequest = (
  parentId: string,
  values: Record<string, unknown>,
  milestoneTypeId: number | undefined,
  completedStatusId?: number,
): CreateProjectTaskRequest => {
  const typeId =
    (values.typeId as number | undefined) ?? DRAFT_TASK_DEFAULTS.typeId
  const isMilestone = typeId === milestoneTypeId
  const has = (field: string) =>
    values[field] !== undefined && values[field] !== null
  const assigneeIds = values.assigneeIds as string[] | undefined
  const plannedDate = (values.plannedDate ?? values.plannedStart) as
    string | undefined
  return {
    name: String(values.name ?? ''),
    parentId,
    typeId,
    statusId:
      (values.statusId as number | undefined) ?? DRAFT_TASK_DEFAULTS.statusId,
    priorityId:
      (values.priorityId as number | undefined) ??
      DRAFT_TASK_DEFAULTS.priorityId,
    ...(assigneeIds?.length ? { assigneeIds } : {}),
    ...(isMilestone
      ? plannedDate
        ? { plannedDate }
        : {}
      : {
          progress:
            completedStatusId !== undefined &&
            values.statusId === completedStatusId
              ? 100
              : ((values.progress as number | null | undefined) ??
                DRAFT_TASK_DEFAULTS.progress),
          ...(has('plannedStart')
            ? { plannedStart: values.plannedStart as string }
            : {}),
          ...(has('plannedEnd')
            ? { plannedEnd: values.plannedEnd as string }
            : {}),
          ...(has('estimatedEffortHours')
            ? { estimatedEffortHours: values.estimatedEffortHours as number }
            : {}),
        }),
  }
}
