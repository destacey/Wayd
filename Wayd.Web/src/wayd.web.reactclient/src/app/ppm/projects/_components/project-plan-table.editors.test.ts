import type { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import {
  DRAFT_TASK_DEFAULTS,
  PLAN_FIELD_TO_COLUMN,
  planAssigneesEditor,
  planEffortEditor,
  planEndEditor,
  planNameEditor,
  planPriorityEditor,
  planProgressEditor,
  planStartEditor,
  planStatusEditor,
  planTypeEditor,
  toCreateProjectTaskRequest,
  toProjectPlanPatch,
  withCompletionProgress,
  asMilestoneDate,
} from './project-plan-table.editors'

const MILESTONE_TYPE_ID = 2

const node = (extra: Partial<ProjectPlanNodeDto> = {}): ProjectPlanNodeDto => ({
  id: 't1',
  nodeType: 'Task',
  key: 'P-1',
  wbs: '1.1',
  name: 'Design',
  type: { id: 1, name: 'Task' },
  status: { id: 1, name: 'Not Started' },
  priority: { id: 2, name: 'Medium' },
  order: 1,
  start: '2026-03-01',
  end: '2026-03-31',
  progress: 10,
  assignees: [],
  children: [],
  deliverableIds: [],
  ...extra,
})

const stage = (extra: Partial<ProjectPlanNodeDto> = {}) =>
  node({
    id: 's1',
    nodeType: 'Stage',
    name: 'Build',
    type: undefined,
    priority: undefined,
    ...extra,
  })

const milestone = (extra: Partial<ProjectPlanNodeDto> = {}) =>
  node({
    id: 'm1',
    type: { id: MILESTONE_TYPE_ID, name: 'Milestone' },
    start: undefined,
    end: undefined,
    plannedDate: '2026-04-15',
    ...extra,
  })

const DRAFT = { isDraft: true }
const EXISTING = { isDraft: false }

const STATUS_OPTIONS = [
  { label: 'Not Started', value: 1 },
  { label: 'In Progress', value: 2 },
  { label: 'Completed', value: 3 },
]
const MILESTONE_STATUS_OPTIONS = [
  { label: 'Not Started', value: 1 },
  { label: 'Completed', value: 3 },
]
const TYPE_OPTIONS = [
  { label: 'Task', value: 1 },
  { label: 'Milestone', value: MILESTONE_TYPE_ID },
]
const PRIORITY_OPTIONS = [
  { label: 'Low', value: 1 },
  { label: 'Medium', value: 2 },
]
const EMPLOYEES = [
  { label: 'Ada Lovelace', value: 'e1' },
  { label: 'Bo Diddley', value: 'e2' },
]

/** The options a select editor offers on a row. */
const choicesOn = (
  editor: { render: unknown },
  row: ProjectPlanNodeDto,
): unknown => {
  const element = (editor.render as (props: object) => { props: any })({
    value: null,
    row,
  })
  const choices = element.props.choices
  return typeof choices === 'function' ? choices(row) : choices
}

describe('project plan editors', () => {
  describe('name', () => {
    it('edits a task and a draft but not a stage', () => {
      // Arrange / Act
      const editable = [node(), stage()].map((row) =>
        planNameEditor.editable!(row, EXISTING),
      )

      // Assert
      expect(editable).toStrictEqual([true, false])
      expect(planNameEditor.editable!(node({ id: 'draft-1' }), DRAFT)).toBe(
        true,
      )
    })

    it('requires a name', () => {
      // Arrange / Act
      const message = planNameEditor.validate!('   ', node())

      // Assert
      expect(message).toBe('Name is required.')
    })

    it('reads and writes the name', () => {
      // Arrange / Act
      const renamed = planNameEditor.apply(node(), 'Renamed')

      // Assert
      expect(planNameEditor.get(renamed)).toBe('Renamed')
    })
  })

  describe('type', () => {
    it('is chosen only on a draft', () => {
      // Arrange
      const editor = planTypeEditor(TYPE_OPTIONS)

      // Act / Assert
      expect(editor.editable!(node(), EXISTING)).toBe(false)
      expect(editor.editable!(node({ id: 'draft-1' }), DRAFT)).toBe(true)
    })

    it('turns the draft into a milestone, which its other cells read', () => {
      // Arrange
      const editor = planTypeEditor(TYPE_OPTIONS)

      // Act
      const row = editor.apply(node({ id: 'draft-1' }), MILESTONE_TYPE_ID)

      // Assert
      expect(row.type).toStrictEqual({
        id: MILESTONE_TYPE_ID,
        name: 'Milestone',
      })
      expect(editor.get(row)).toBe(MILESTONE_TYPE_ID)
      expect(planEndEditor.editable!(row, DRAFT)).toBe(false)
      expect(planProgressEditor.editable!(row, DRAFT)).toBe(false)
    })
  })

  describe('status', () => {
    const editor = planStatusEditor(STATUS_OPTIONS, MILESTONE_STATUS_OPTIONS)

    it('refuses In Progress on a draft that became a milestone', () => {
      // Arrange / Act / Assert
      expect(editor.validate!(2, milestone({ id: 'draft-1' }))).toBe(
        'A milestone can only be Not Started or Completed.',
      )
      expect(editor.validate!(3, milestone({ id: 'draft-1' }))).toBeUndefined()
      expect(editor.validate!(2, node())).toBeUndefined()
    })

    it('checks nothing before the milestone statuses load', () => {
      // Arrange
      const loading = planStatusEditor(STATUS_OPTIONS, [])

      // Act / Assert
      expect(loading.validate!(2, milestone())).toBeUndefined()
    })

    it('offers a milestone only the statuses a milestone can have', () => {
      // Arrange / Act
      const choices = choicesOn(editor, milestone())

      // Assert
      expect(choices).toStrictEqual(MILESTONE_STATUS_OPTIONS)
    })

    it('offers a task every status', () => {
      // Arrange / Act
      const choices = choicesOn(editor, node())

      // Assert
      expect(choices).toStrictEqual(STATUS_OPTIONS)
    })

    it('edits a stage too, and shows the chosen status by name', () => {
      // Arrange / Act
      const row = editor.apply(stage(), 3)

      // Assert
      expect(editor.editable?.(stage(), EXISTING) ?? true).toBe(true)
      expect(row.status).toStrictEqual({ id: 3, name: 'Completed' })
      expect(editor.get(row)).toBe(3)
    })
  })

  describe('priority', () => {
    const editor = planPriorityEditor(PRIORITY_OPTIONS)

    it('edits a task but not a stage', () => {
      // Arrange / Act / Assert
      expect(editor.editable!(node(), EXISTING)).toBe(true)
      expect(editor.editable!(stage(), EXISTING)).toBe(false)
    })

    it('shows the chosen priority by name', () => {
      // Arrange / Act
      const row = editor.apply(node(), 1)

      // Assert
      expect(row.priority).toStrictEqual({ id: 1, name: 'Low' })
      expect(editor.get(row)).toBe(1)
    })
  })

  describe('assignees', () => {
    const editor = planAssigneesEditor(EMPLOYEES)

    it('reads the assignee ids', () => {
      // Arrange
      const row = node({
        assignees: [{ id: 'e2', key: 2, name: 'Bo Diddley' } as never],
      })

      // Act / Assert
      expect(editor.get(row)).toStrictEqual(['e2'])
    })

    it('shows the chosen people by name', () => {
      // Arrange / Act
      const row = editor.apply(node(), ['e1', 'e2'])

      // Assert
      expect(row.assignees.map((a) => a.name)).toStrictEqual([
        'Ada Lovelace',
        'Bo Diddley',
      ])
      expect(editor.get(row)).toStrictEqual(['e1', 'e2'])
    })

    it('chooses several people', () => {
      // Arrange
      const element = (editor.render as (p: object) => { props: any })({
        value: [],
        row: node(),
      })

      // Act / Assert
      expect(element.props.multiple).toBe(true)
    })
  })

  describe('progress', () => {
    it('edits tasks and stages, not milestones', () => {
      // Arrange / Act / Assert
      expect(planProgressEditor.editable!(node(), EXISTING)).toBe(true)
      expect(planProgressEditor.editable!(stage(), EXISTING)).toBe(true)
      expect(planProgressEditor.editable!(milestone(), EXISTING)).toBe(false)
    })

    it('stays between 0 and 100 and cannot be empty', () => {
      // Arrange / Act / Assert
      expect(planProgressEditor.validate!(null, node())).toBe(
        'Progress is required.',
      )
      expect(planProgressEditor.validate!(101, node())).toBe(
        'Progress must be between 0 and 100.',
      )
      expect(planProgressEditor.validate!(-1, node())).toBe(
        'Progress must be between 0 and 100.',
      )
      expect(planProgressEditor.validate!(100, node())).toBeUndefined()
      expect(planProgressEditor.validate!(0, node())).toBeUndefined()
    })
  })

  describe('estimated effort', () => {
    it('edits tasks only', () => {
      // Arrange / Act / Assert
      expect(planEffortEditor.editable!(node(), EXISTING)).toBe(true)
      expect(planEffortEditor.editable!(stage(), EXISTING)).toBe(false)
      expect(planEffortEditor.editable!(milestone(), EXISTING)).toBe(false)
    })

    it('must be more than zero, or empty', () => {
      // Arrange / Act / Assert
      expect(planEffortEditor.validate!(0, node())).toBe(
        'Estimated effort must be greater than 0.',
      )
      expect(planEffortEditor.validate!(null, node())).toBeUndefined()
      expect(planEffortEditor.validate!(0.25, node())).toBeUndefined()
    })

    it('clears to no estimate', () => {
      // Arrange / Act
      const row = planEffortEditor.apply(
        node({ estimatedEffortHours: 8 }),
        null,
      )

      // Assert
      expect(planEffortEditor.get(row)).toBeNull()
    })
  })

  describe('planned dates', () => {
    const check = (
      values: { start: string | null; end: string | null },
      row: ProjectPlanNodeDto,
    ) => planStartEditor.group!.validate!(values, row)

    it('edit a milestone’s single date in Start, as its planned date', () => {
      // Arrange
      const row = milestone()

      // Act
      const moved = planStartEditor.apply(row, '2026-04-20')

      // Assert
      expect(planStartEditor.field).toBeInstanceOf(Function)
      expect((planStartEditor.field as (r: unknown) => string)(row)).toBe(
        'plannedDate',
      )
      expect(planStartEditor.get(row)).toBe('2026-04-15')
      expect(moved.plannedDate).toBe('2026-04-20')
      expect(planEndEditor.editable!(row, EXISTING)).toBe(false)
    })

    it('edit a task’s Start as its planned start', () => {
      // Arrange
      const row = node()

      // Act
      const moved = planStartEditor.apply(row, '2026-03-05')

      // Assert
      expect((planStartEditor.field as (r: unknown) => string)(row)).toBe(
        'plannedStart',
      )
      expect(moved.start).toBe('2026-03-05')
      expect(planEndEditor.get(planEndEditor.apply(row, '2026-04-01'))).toBe(
        '2026-04-01',
      )
    })

    it('require a milestone’s date', () => {
      // Arrange / Act / Assert
      expect(planStartEditor.validate!(null, milestone())).toBe(
        'A milestone needs a date.',
      )
      expect(planStartEditor.validate!(null, node())).toBeUndefined()
    })

    it('need a milestone draft’s date to create it, not a task draft’s', () => {
      // Arrange
      const required = planStartEditor.requiredToCreate as (
        row: ProjectPlanNodeDto,
      ) => boolean

      // Act / Assert
      expect(required(milestone({ id: 'draft-1' }))).toBe(true)
      expect(required(node({ id: 'draft-1' }))).toBe(false)
      expect(planEndEditor.requiredToCreate).toBeFalsy()
    })

    it('need both dates or neither', () => {
      // Arrange
      const message =
        'Planned Start and Planned End must both have a value or both be empty.'

      // Act / Assert
      expect(check({ start: '2026-03-01', end: null }, node())).toStrictEqual({
        start: message,
        end: message,
      })
      expect(check({ start: null, end: null }, node())).toBeUndefined()
    })

    it('do not let the end come before the start', () => {
      // Arrange / Act
      const errors = check({ start: '2026-03-10', end: '2026-03-09' }, node())

      // Assert
      expect(errors).toStrictEqual({
        end: 'Planned End cannot be earlier than Planned Start.',
      })
    })

    it('leave a milestone’s date to its own check', () => {
      // Arrange / Act — a milestone has a date and never an End
      const errors = check({ start: '2026-04-15', end: null }, milestone())

      // Assert
      expect(errors).toBeUndefined()
    })

    describe('of a parent', () => {
      // A stage from Mar 1 to Mar 31 whose task runs Mar 10 to Mar 20.
      const parent = stage({
        start: '2026-03-01',
        end: '2026-03-31',
        children: [
          node({
            id: 'c1',
            name: 'Child',
            start: '2026-03-10',
            end: '2026-03-20',
          }),
        ],
      })

      it('allow a shift that carries the children along', () => {
        // Arrange / Act — both ends move 15 days, past the child
        const errors = check({ start: '2026-03-16', end: '2026-04-15' }, parent)

        // Assert
        expect(errors).toBeUndefined()
      })

      it('mark the start when a resize cuts a child off at the start', () => {
        // Arrange / Act
        const errors = check({ start: '2026-03-12', end: '2026-03-31' }, parent)

        // Assert
        expect(Object.keys(errors ?? {})).toStrictEqual(['start'])
        expect(errors!.start).toContain('Child')
      })

      it('mark the end when a resize cuts a child off at the end', () => {
        // Arrange / Act
        const errors = check({ start: '2026-03-01', end: '2026-03-15' }, parent)

        // Assert
        expect(Object.keys(errors ?? {})).toStrictEqual(['end'])
      })

      it('allow a resize that still contains the children', () => {
        // Arrange / Act
        const errors = check({ start: '2026-03-05', end: '2026-03-25' }, parent)

        // Assert
        expect(errors).toBeUndefined()
      })

      it('do not clear dates while a child has dates', () => {
        // Arrange
        const message =
          'Planned dates cannot be cleared when child items have dates.'

        // Act
        const errors = check({ start: null, end: null }, parent)

        // Assert
        expect(errors).toStrictEqual({ start: message, end: message })
      })

      it('count a milestone child by its date', () => {
        // Arrange
        const withMilestone = stage({
          start: '2026-03-01',
          end: '2026-03-31',
          children: [milestone({ plannedDate: '2026-03-28' })],
        })

        // Act
        const errors = check(
          { start: '2026-03-01', end: '2026-03-20' },
          withMilestone,
        )

        // Assert
        expect(Object.keys(errors ?? {})).toStrictEqual(['end'])
      })
    })
  })
})

describe('toProjectPlanPatch', () => {
  it('sends a task’s changed field by its task path', () => {
    // Arrange / Act
    const operations = toProjectPlanPatch(node(), { statusId: 3 })

    // Assert
    expect(operations).toStrictEqual([
      { op: 'replace', path: '/StatusId', value: 3 },
    ])
  })

  it('sends a stage’s status by the stage path', () => {
    // Arrange / Act
    const operations = toProjectPlanPatch(stage(), { statusId: 3 })

    // Assert
    expect(operations).toStrictEqual([
      { op: 'replace', path: '/Status', value: 3 },
    ])
  })

  it('sends both dates of a pair in one patch', () => {
    // Arrange / Act
    const operations = toProjectPlanPatch(node(), {
      plannedStart: '2026-03-05',
      plannedEnd: '2026-04-04',
    })

    // Assert
    expect(operations).toStrictEqual([
      { op: 'replace', path: '/PlannedStart', value: '2026-03-05' },
      { op: 'replace', path: '/PlannedEnd', value: '2026-04-04' },
    ])
  })

  it('sends a cleared value as null', () => {
    // Arrange / Act
    const operations = toProjectPlanPatch(node(), {
      estimatedEffortHours: null,
    })

    // Assert
    expect(operations).toStrictEqual([
      { op: 'replace', path: '/EstimatedEffortHours', value: null },
    ])
  })

  it.each([
    ['name', 'Renamed'],
    ['statusId', 2],
    ['priorityId', 1],
    ['assigneeIds', ['e1']],
    ['plannedStart', '2026-03-05'],
    ['plannedEnd', '2026-03-06'],
    ['plannedDate', '2026-03-07'],
    ['progress', 50],
    ['estimatedEffortHours', 4],
  ])('has a task path for the %s field', (field, value) => {
    // Arrange / Act
    const operations = toProjectPlanPatch(node(), { [field]: value })

    // Assert
    expect(operations).toHaveLength(1)
  })

  it.each([
    ['statusId', 2],
    ['assigneeIds', ['e1']],
    ['plannedStart', '2026-03-05'],
    ['plannedEnd', '2026-03-06'],
    ['progress', 50],
  ])('has a stage path for the %s field', (field, value) => {
    // Arrange / Act
    const operations = toProjectPlanPatch(stage(), { [field]: value })

    // Assert
    expect(operations).toHaveLength(1)
  })

  it('throws on a field it has no path for, rather than sending nothing', () => {
    // Arrange / Act / Assert
    expect(() => toProjectPlanPatch(stage(), { name: 'Renamed' })).toThrow(
      'name',
    )
  })
})

describe('toCreateProjectTaskRequest', () => {
  it('creates a task from the staged values and the draft’s defaults', () => {
    // Arrange / Act
    const request = toCreateProjectTaskRequest(
      's1',
      { name: 'New task', priorityId: 1, assigneeIds: ['e1'] },
      MILESTONE_TYPE_ID,
    )

    // Assert
    expect(request).toStrictEqual({
      name: 'New task',
      parentId: 's1',
      typeId: DRAFT_TASK_DEFAULTS.typeId,
      statusId: DRAFT_TASK_DEFAULTS.statusId,
      priorityId: 1,
      assigneeIds: ['e1'],
      progress: DRAFT_TASK_DEFAULTS.progress,
    })
  })

  it('sends a task’s dates, progress and effort', () => {
    // Arrange / Act
    const request = toCreateProjectTaskRequest(
      's1',
      {
        name: 'Dated',
        plannedStart: '2026-03-01',
        plannedEnd: '2026-03-05',
        progress: 20,
        estimatedEffortHours: 6,
      },
      MILESTONE_TYPE_ID,
    )

    // Assert
    expect(request).toStrictEqual({
      name: 'Dated',
      parentId: 's1',
      typeId: 1,
      statusId: 1,
      priorityId: 2,
      progress: 20,
      plannedStart: '2026-03-01',
      plannedEnd: '2026-03-05',
      estimatedEffortHours: 6,
    })
  })

  it('creates a milestone with its date and no progress or range', () => {
    // Arrange / Act
    const request = toCreateProjectTaskRequest(
      't1',
      {
        name: 'Go live',
        typeId: MILESTONE_TYPE_ID,
        plannedDate: '2026-04-15',
        progress: 30,
        plannedEnd: '2026-04-20',
      },
      MILESTONE_TYPE_ID,
    )

    // Assert
    expect(request).toStrictEqual({
      name: 'Go live',
      parentId: 't1',
      typeId: MILESTONE_TYPE_ID,
      statusId: 1,
      priorityId: 2,
      plannedDate: '2026-04-15',
    })
  })

  it('takes a Start staged before the draft became a milestone as its date', () => {
    // Arrange / Act
    const request = toCreateProjectTaskRequest(
      't1',
      {
        name: 'Go live',
        typeId: MILESTONE_TYPE_ID,
        plannedStart: '2026-04-15',
      },
      MILESTONE_TYPE_ID,
    )

    // Assert
    expect(request.plannedDate).toBe('2026-04-15')
    expect(request).not.toHaveProperty('plannedStart')
  })
})

describe('a draft’s date', () => {
  it('keeps one key whatever the draft’s type', () => {
    // Arrange
    const field = planStartEditor.field as (row: ProjectPlanNodeDto) => string

    // Act / Assert
    expect(field(milestone({ id: 'draft-1' }))).toBe('plannedStart')
    expect(field(node({ id: 'draft-1' }))).toBe('plannedStart')
  })

  it('shows on the draft whether it ends up a task or a milestone', () => {
    // Arrange
    const draft = node({ id: 'draft-1', start: undefined, end: undefined })

    // Act
    const dated = planStartEditor.apply(draft, '2026-05-01')
    const asMilestone = planTypeEditor(TYPE_OPTIONS).apply(
      dated,
      MILESTONE_TYPE_ID,
    )

    // Assert
    expect(planStartEditor.get(dated)).toBe('2026-05-01')
    expect(planStartEditor.get(asMilestone)).toBe('2026-05-01')
  })
})

describe('asMilestoneDate', () => {
  it('sends a milestone’s Start as its planned date', () => {
    // Arrange / Act
    const patch = asMilestoneDate(milestone(), {
      plannedStart: '2026-05-01',
      plannedEnd: '2026-05-02',
      name: 'x',
    })

    // Assert
    expect(patch).toStrictEqual({ name: 'x', plannedDate: '2026-05-01' })
  })

  it('leaves a task’s dates alone', () => {
    // Arrange / Act
    const patch = asMilestoneDate(node(), { plannedStart: '2026-05-01' })

    // Assert
    expect(patch).toStrictEqual({ plannedStart: '2026-05-01' })
  })
})

describe('withCompletionProgress', () => {
  const COMPLETED = 3

  it('makes a task set to Completed 100% done', () => {
    // Arrange / Act
    const patch = withCompletionProgress(node(), { statusId: 3 }, COMPLETED)

    // Assert
    expect(patch).toStrictEqual({ statusId: 3, progress: 100 })
  })

  it('makes a stage set to Completed 100% done', () => {
    // Arrange / Act
    const patch = withCompletionProgress(stage(), { statusId: 3 }, COMPLETED)

    // Assert
    expect(patch).toStrictEqual({ statusId: 3, progress: 100 })
  })

  it('leaves a milestone to the server, which completes it itself', () => {
    // Arrange / Act
    const patch = withCompletionProgress(
      milestone(),
      { statusId: 3 },
      COMPLETED,
    )

    // Assert
    expect(patch).toStrictEqual({ statusId: 3 })
  })

  it('leaves other statuses and other fields alone', () => {
    // Arrange / Act / Assert
    expect(
      withCompletionProgress(node(), { statusId: 2 }, COMPLETED),
    ).toStrictEqual({
      statusId: 2,
    })
    expect(
      withCompletionProgress(node(), { name: 'x' }, COMPLETED),
    ).toStrictEqual({
      name: 'x',
    })
    expect(
      withCompletionProgress(node(), { statusId: 3 }, undefined),
    ).toStrictEqual({
      statusId: 3,
    })
  })
})

describe('toCreateProjectTaskRequest when Completed', () => {
  it('creates a completed task 100% done', () => {
    // Arrange / Act
    const request = toCreateProjectTaskRequest(
      's1',
      { name: 'Done already', statusId: 3, progress: 20 },
      MILESTONE_TYPE_ID,
      3,
    )

    // Assert
    expect(request.progress).toBe(100)
  })
})

describe('PLAN_FIELD_TO_COLUMN', () => {
  it('puts a milestone’s date errors and a stage’s status errors on their columns', () => {
    // Arrange / Act / Assert
    expect(PLAN_FIELD_TO_COLUMN).toStrictEqual({
      plannedStart: 'plannedStart',
      plannedDate: 'plannedStart',
      status: 'status',
    })
  })
})
