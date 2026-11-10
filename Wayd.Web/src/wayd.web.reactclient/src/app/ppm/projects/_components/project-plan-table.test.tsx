const mockMessage = {
  success: vi.fn(),
  error: vi.fn(),
  warning: vi.fn(),
  info: vi.fn(),
}
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

const mockPatchTask = vi.fn()
const mockPatchStage = vi.fn()
const mockCreateTask = vi.fn()
const mockPlaceTask = vi.fn()
vi.mock('@/src/store/features/ppm/project-tasks-api', () => ({
  useGetTaskStatusOptionsQuery: (arg?: { forMilestone?: boolean }) => ({
    data: arg?.forMilestone
      ? [
          { label: 'Not Started', value: 1 },
          { label: 'Completed', value: 3 },
        ]
      : [
          { label: 'Not Started', value: 1 },
          { label: 'In Progress', value: 2 },
          { label: 'Completed', value: 3 },
        ],
  }),
  useGetTaskPriorityOptionsQuery: () => ({
    data: [
      { label: 'Low', value: 1 },
      { label: 'Medium', value: 2 },
      { label: 'High', value: 3 },
    ],
  }),
  useGetTaskTypeOptionsQuery: () => ({
    data: [
      { label: 'Task', value: 1 },
      { label: 'Milestone', value: 2 },
    ],
  }),
  usePatchProjectTaskMutation: () => [mockPatchTask],
  useUpdateProjectTaskPlacementMutation: () => [mockPlaceTask],
  useCreateProjectTaskMutation: () => [mockCreateTask],
}))
vi.mock('@/src/store/features/ppm/projects-api', () => ({
  usePatchProjectStageMutation: () => [mockPatchStage],
}))
vi.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: () => ({
    data: [
      { label: 'Ada Lovelace', value: 'e1' },
      { label: 'Bo Diddley', value: 'e2' },
    ],
  }),
}))
vi.mock('@/src/store/features/ppm/project-deliverables-api', () => ({
  useGetProjectDeliverablesQuery: () => ({ data: [] }),
}))

// A dragged bar's drop, handed to the test to call.
let mockBarCommit:
  ((change: { id: string; start: number; end: number }) => unknown) | undefined
vi.mock('@/src/components/common/timeline', async () => ({
  ...(await vi.importActual<any>('@/src/components/common/timeline')),
  useGanttVisibility: () => ({ visible: false, toggle: vi.fn() }),
  useBarDrag: (options: { onCommit: typeof mockBarCommit }) => {
    mockBarCommit = options.onCommit
    return { active: null, start: vi.fn() }
  },
}))

vi.mock('./delete-project-task-form', () => ({
  default: ({
    taskIdOrKey,
    onFormComplete,
    onFormCancel,
  }: {
    taskIdOrKey: string
    onFormComplete: () => void
    onFormCancel: () => void
  }) => (
    <div role="dialog" aria-label="Delete task">
      {taskIdOrKey}
      <button onClick={onFormComplete}>Confirm delete</button>
      <button onClick={onFormCancel}>Cancel delete</button>
    </div>
  ),
}))
vi.mock('./project-plan-item-drawer', () => ({
  default: ({
    drawerOpen,
    taskId,
  }: {
    drawerOpen: boolean
    taskId: string | null
  }) =>
    drawerOpen ? (
      <div role="dialog" aria-label="Plan item">
        {taskId}
      </div>
    ) : null,
}))
vi.mock('./create-project-task-form', () => ({
  default: () => null,
}))
vi.mock('./edit-project-task-form', () => ({
  default: () => null,
}))
vi.mock('./edit-project-stage-form', () => ({
  default: () => null,
}))

import { act, fireEvent, render, screen, within } from '@testing-library/react'
import dayjs from 'dayjs'
import type { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import ProjectPlanTable from './project-plan-table'

const task = (extra: Partial<ProjectPlanNodeDto>): ProjectPlanNodeDto => ({
  id: 'x',
  nodeType: 'Task',
  key: 'P-x',
  wbs: '1',
  name: 'Task',
  type: { id: 1, name: 'Task' },
  status: { id: 1, name: 'Not Started' },
  priority: { id: 2, name: 'Medium' },
  order: 1,
  progress: 0,
  assignees: [],
  children: [],
  deliverableIds: [],
  ...extra,
})

// Build (Mar 1 – Mar 31)
//   Design (Mar 10 – Mar 20)
//   Go live (milestone, Mar 25)
//   Wire up (no dates)
// Test (no dates, no tasks)
const PLAN: ProjectPlanNodeDto[] = [
  task({
    id: 's1',
    nodeType: 'Stage',
    key: undefined,
    name: 'Build',
    type: undefined,
    priority: undefined,
    start: '2026-03-01',
    end: '2026-03-31',
    children: [
      task({
        id: 't1',
        key: 'P-1',
        name: 'Design',
        order: 1,
        start: '2026-03-10',
        end: '2026-03-20',
      }),
      task({
        id: 'm1',
        key: 'P-2',
        name: 'Go live',
        order: 2,
        type: { id: 2, name: 'Milestone' },
        plannedDate: '2026-03-25',
      }),
      task({ id: 't2', key: 'P-3', name: 'Wire up', order: 3 }),
    ],
  }),
  task({
    id: 's2',
    nodeType: 'Stage',
    key: undefined,
    name: 'Test',
    type: undefined,
    priority: undefined,
    order: 2,
  }),
]

const ROW = { s1: 0, t1: 1, m1: 2, t2: 3, s2: 4 } as const

const resolved = (value?: unknown) => ({
  unwrap: () => Promise.resolve(value),
})
const rejected = (error: unknown) => ({ unwrap: () => Promise.reject(error) })

const renderPlan = (canManageTasks = true) =>
  render(
    <ProjectPlanTable
      projectId="p-id"
      projectKey="P"
      tasks={PLAN}
      isLoading={false}
      canManageTasks={canManageTasks}
      refetch={vi.fn(() => Promise.resolve())}
    />,
  )

const bodyCells = (columnId: string) =>
  Array.from(
    document.querySelectorAll(`tbody td[data-column-id="${columnId}"]`),
  ) as HTMLTableCellElement[]
const cellAt = (rowIndex: number, columnId: string) =>
  bodyCells(columnId)[rowIndex]
const focus = (el: HTMLElement) => act(() => el.focus())
const press = (key: string, options: Partial<KeyboardEventInit> = {}) =>
  fireEvent.keyDown(document.activeElement!, { key, ...options })
const typeInEditor = (value: string) =>
  fireEvent.change(document.activeElement!, { target: { value } })
const typeDate = (value: string) =>
  fireEvent.input(document.activeElement!, { target: { value } })
const flush = () => act(async () => {})
const openEditor = () =>
  document.querySelector<HTMLElement>('tbody [data-grid-cell-editor]')

/** Opens a cell and replaces its text. */
const editText = (row: number, columnId: string, value: string) => {
  focus(cellAt(row, columnId))
  press('Enter')
  typeInEditor(value)
}

/** Opens a date cell and types a date into it. */
const editDate = (row: number, columnId: string, value: string) => {
  focus(cellAt(row, columnId))
  press('Enter')
  typeDate(value)
}

/** Picks an option from the open editor's list. */
const pick = async (label: string) => {
  const host = document.querySelector<HTMLElement>('[data-grid-editor-owner]')!
  const option = within(host).getByTitle(label)
  fireEvent.click(option)
  await flush()
}

const ms = (date: string) => dayjs(date).valueOf()

describe('ProjectPlanTable cell editing', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPatchTask.mockReturnValue(resolved())
    mockPatchStage.mockReturnValue(resolved())
    mockCreateTask.mockReturnValue(resolved({ id: 'new-1', key: 'P-9' }))
    mockPlaceTask.mockReturnValue(resolved())
  })

  describe('committing a cell', () => {
    it('sends only the changed field of a task, as JSON Patch', async () => {
      // Arrange
      renderPlan()
      editText(ROW.t1, 'name', 'Design review')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockPatchTask.mock.calls).toStrictEqual([
        [
          {
            projectIdOrKey: 'P',
            taskId: 't1',
            cacheKey: 't1',
            patchOperations: [
              { op: 'replace', path: '/Name', value: 'Design review' },
            ],
          },
        ],
      ])
    })

    it('saves a stage through the stage endpoint', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.s1, 'status'))
      press('Enter')

      // Act
      await pick('Completed')

      // Assert
      expect(mockPatchTask).not.toHaveBeenCalled()
      expect(mockPatchStage.mock.calls).toStrictEqual([
        [
          {
            projectId: 'p-id',
            projectKey: 'P',
            stageId: 's1',
            patchOperations: [
              { op: 'replace', path: '/Status', value: 3 },
              { op: 'replace', path: '/Progress', value: 100 },
            ],
          },
        ],
      ])
    })

    it('saves a priority', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t1, 'priority'))
      press('Enter')

      // Act
      await pick('High')

      // Assert
      expect(mockPatchTask.mock.calls[0][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/PriorityId', value: 3 },
      ])
    })

    it('saves several assignees in one patch when the cell is left', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t1, 'assignees'))
      press('Enter')
      await pick('Ada Lovelace')
      await pick('Bo Diddley')

      // Act
      fireEvent.pointerDown(
        document.querySelector('[data-grid-body-viewport]')!,
      )
      await flush()

      // Assert
      expect(mockPatchTask.mock.calls).toHaveLength(1)
      expect(mockPatchTask.mock.calls[0][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/AssigneeIds', value: ['e1', 'e2'] },
      ])
      expect(cellAt(ROW.t1, 'assignees')).toHaveTextContent('Ada Lovelace')
    })

    it('saves progress and estimated effort', async () => {
      // Arrange
      renderPlan()
      editText(ROW.t1, 'progress', '40')
      press('Enter')
      await flush()

      // Act
      editText(ROW.t1, 'estimatedEffortHours', '6')
      press('Enter')
      await flush()

      // Assert
      expect(
        mockPatchTask.mock.calls.map((call) => call[0].patchOperations),
      ).toStrictEqual([
        [{ op: 'replace', path: '/Progress', value: 40 }],
        [{ op: 'replace', path: '/EstimatedEffortHours', value: 6 }],
      ])
    })

    it('makes a task set to Completed 100% done', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t1, 'status'))
      press('Enter')

      // Act
      await pick('Completed')

      // Assert
      expect(mockPatchTask.mock.calls[0][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/StatusId', value: 3 },
        { op: 'replace', path: '/Progress', value: 100 },
      ])
    })

    it('shows no toast when a change saves', async () => {
      // Arrange
      renderPlan()
      editText(ROW.t1, 'name', 'Design review')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockPatchTask).toHaveBeenCalledTimes(1)
      expect(mockMessage.success).not.toHaveBeenCalled()
      expect(mockMessage.error).not.toHaveBeenCalled()
    })

    it('places a 422 field error on the cell it names', async () => {
      // Arrange
      mockPatchTask.mockReturnValue(
        rejected({
          status: 422,
          data: {
            status: 422,
            errors: { EstimatedEffortHours: ['Too much effort.'] },
          },
        }),
      )
      renderPlan()
      editText(ROW.t1, 'estimatedEffortHours', '9')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(
        cellAt(ROW.t1, 'estimatedEffortHours'),
      ).toHaveAccessibleDescription('Est Effort (hrs): Too much effort.')
      expect(mockMessage.error).not.toHaveBeenCalled()
    })

    it('places a stage’s Status error on its Status cell', async () => {
      // Arrange
      mockPatchStage.mockReturnValue(
        rejected({
          status: 422,
          data: { status: 422, errors: { Status: ['Not allowed.'] } },
        }),
      )
      renderPlan()
      focus(cellAt(ROW.s1, 'status'))
      press('Enter')

      // Act
      await pick('Completed')

      // Assert
      expect(cellAt(ROW.s1, 'status')).toHaveAccessibleDescription(
        'Status: Not allowed.',
      )
    })

    it('does not pick the highlighted status with Tab, and opens the next cell', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t1, 'status'))
      press('Enter')
      const combobox = screen.getByRole('combobox', { name: 'Status' })
      fireEvent.keyDown(combobox, { key: 'ArrowDown', keyCode: 40, which: 40 })

      // Act
      fireEvent.keyDown(combobox, { key: 'Tab', keyCode: 9, which: 9 })
      await flush()

      // Assert
      expect(mockPatchTask).not.toHaveBeenCalled()
      expect(cellAt(ROW.t1, 'status')).toHaveTextContent('Not Started')
      expect(cellAt(ROW.t1, 'priority')).toContainElement(openEditor())
    })

    it('edits nothing for a user who cannot manage the plan', () => {
      // Arrange
      renderPlan(false)
      focus(cellAt(ROW.t1, 'name'))

      // Act
      press('Enter')

      // Assert
      expect(openEditor()).toBeNull()
    })

    it('does not edit a stage’s name or priority', () => {
      // Arrange
      renderPlan()

      // Act
      focus(cellAt(ROW.s1, 'name'))
      press('Enter')
      const nameOpened = !!openEditor()
      focus(cellAt(ROW.s1, 'priority'))
      press('Enter')

      // Assert
      expect(nameOpened).toBe(false)
      expect(openEditor()).toBeNull()
    })
  })

  describe('planned dates', () => {
    it('sends Start and End together after Tab moves between them', async () => {
      // Arrange
      renderPlan()
      editDate(ROW.t1, 'plannedStart', '2026-03-11')
      press('Tab')
      typeDate('2026-03-22')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockPatchTask.mock.calls).toHaveLength(1)
      expect(mockPatchTask.mock.calls[0][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/PlannedStart', value: '2026-03-11' },
        { op: 'replace', path: '/PlannedEnd', value: '2026-03-22' },
      ])
    })

    it('sends a milestone’s Start as its planned date', async () => {
      // Arrange
      renderPlan()
      editDate(ROW.m1, 'plannedStart', '2026-03-27')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockPatchTask.mock.calls[0][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/PlannedDate', value: '2026-03-27' },
      ])
    })

    it('does not edit a milestone’s End', () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.m1, 'plannedEnd'))

      // Act
      press('Enter')

      // Assert
      expect(openEditor()).toBeNull()
    })

    it('places a milestone’s PlannedDate error on its Start cell', async () => {
      // Arrange
      mockPatchTask.mockReturnValue(
        rejected({
          status: 422,
          data: { status: 422, errors: { PlannedDate: ['Too late.'] } },
        }),
      )
      renderPlan()
      editText(ROW.m1, 'name', 'Launch')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(cellAt(ROW.m1, 'plannedStart')).toHaveAccessibleDescription(
        'Planned Start: Too late.',
      )
    })

    it('keeps a stage resize that cuts a task off on the cell, unsent', async () => {
      // Arrange — Design starts Mar 10
      renderPlan()
      editDate(ROW.s1, 'plannedStart', '2026-03-12')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockPatchStage).not.toHaveBeenCalled()
      expect(cellAt(ROW.s1, 'plannedStart')).toHaveAttribute(
        'data-edit-state',
        'error',
      )
      expect(cellAt(ROW.s1, 'plannedStart')).toHaveAccessibleDescription(
        expect.stringContaining('Design'),
      )
    })

    it('sends a stage shift that carries its tasks along', async () => {
      // Arrange
      renderPlan()
      editDate(ROW.s1, 'plannedStart', '2026-03-16')
      press('Tab')
      typeDate('2026-04-15')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockPatchStage.mock.calls[0][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/PlannedStart', value: '2026-03-16' },
        { op: 'replace', path: '/PlannedEnd', value: '2026-04-15' },
      ])
    })
  })

  describe('a dragged Gantt bar', () => {
    it('sends the bar’s dates through the cells', async () => {
      // Arrange
      renderPlan()

      // Act
      act(() => {
        mockBarCommit!({
          id: 't1',
          start: ms('2026-03-12'),
          end: ms('2026-03-22'),
        })
      })
      await flush()

      // Assert
      expect(mockPatchTask.mock.calls[0][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/PlannedStart', value: '2026-03-12' },
        { op: 'replace', path: '/PlannedEnd', value: '2026-03-22' },
      ])
    })

    it('moves a milestone’s planned date', async () => {
      // Arrange
      renderPlan()

      // Act
      act(() => {
        mockBarCommit!({
          id: 'm1',
          start: ms('2026-03-28'),
          end: ms('2026-03-28'),
        })
      })
      await flush()

      // Assert
      expect(mockPatchTask.mock.calls[0][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/PlannedDate', value: '2026-03-28' },
      ])
    })

    it('leaves an editor open on another row as it was', async () => {
      // Arrange
      renderPlan()
      editText(ROW.t2, 'name', 'Wire up the API')

      // Act
      act(() => {
        mockBarCommit!({
          id: 't1',
          start: ms('2026-03-12'),
          end: ms('2026-03-22'),
        })
      })
      await flush()

      // Assert
      expect(cellAt(ROW.t2, 'name')).toContainElement(openEditor())
      expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue(
        'Wire up the API',
      )
      expect(cellAt(ROW.t1, 'plannedStart')).toHaveTextContent('Mar 12, 2026')
    })

    it('replaces a failed date on the dragged row', async () => {
      // Arrange
      mockPatchTask.mockReturnValueOnce(
        rejected({ status: 400, data: { detail: 'Not saved.' } }),
      )
      renderPlan()
      editDate(ROW.t1, 'plannedEnd', '2026-03-21')
      press('Enter')
      await flush()
      const failed = cellAt(ROW.t1, 'plannedEnd').getAttribute(
        'data-edit-state',
      )

      // Act
      act(() => {
        mockBarCommit!({
          id: 't1',
          start: ms('2026-03-12'),
          end: ms('2026-03-22'),
        })
      })
      await flush()

      // Assert
      expect(failed).toBe('error')
      expect(mockPatchTask.mock.calls[1][0].patchOperations).toStrictEqual([
        { op: 'replace', path: '/PlannedStart', value: '2026-03-12' },
        { op: 'replace', path: '/PlannedEnd', value: '2026-03-22' },
      ])
      expect(cellAt(ROW.t1, 'plannedEnd')).not.toHaveAttribute(
        'data-edit-state',
        'error',
      )
    })

    it('snaps a stage resize that cuts a task off back, unsent', async () => {
      // Arrange
      renderPlan()

      // Act
      act(() => {
        mockBarCommit!({
          id: 's1',
          start: ms('2026-03-01'),
          end: ms('2026-03-15'),
        })
      })
      await flush()

      // Assert
      expect(mockPatchStage).not.toHaveBeenCalled()
      expect(cellAt(ROW.s1, 'plannedEnd')).toHaveTextContent('Mar 31, 2026')
      expect(cellAt(ROW.s1, 'plannedEnd')).not.toHaveAttribute(
        'data-edit-state',
      )
      expect(
        screen
          .getAllByRole('status')
          .map((r) => r.textContent?.trim())
          .join(' '),
      ).toContain('Go live')
    })
    it('snaps a bar back when the server refuses the drag, and says why', async () => {
      // Arrange
      mockPatchTask.mockReturnValue(
        rejected({ status: 400, data: { detail: 'Outside the project.' } }),
      )
      renderPlan()

      // Act
      act(() => {
        mockBarCommit!({
          id: 't1',
          start: ms('2026-03-12'),
          end: ms('2026-03-22'),
        })
      })
      await flush()

      // Assert
      expect(cellAt(ROW.t1, 'plannedEnd')).toHaveTextContent('Mar 20, 2026')
      expect(cellAt(ROW.t1, 'plannedEnd')).not.toHaveAttribute(
        'data-edit-state',
      )
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).toContain('Not saved. Outside the project.')
    })
  })

  describe('new tasks', () => {
    /** Adds a draft task under a row from its row menu. */
    const addChild = async (row: number, label: string) => {
      fireEvent.click(
        within(cellAt(row, 'actions')).getByRole('button', {
          name: 'Row actions',
        }),
      )
      await flush()
      fireEvent.click(screen.getByRole('menuitem', { name: label }))
      await flush()
    }

    it('creates a task under its stage with the values staged on it', async () => {
      // Arrange
      renderPlan()
      await addChild(ROW.s2, 'Add Task')
      typeInEditor('Plan tests')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockCreateTask.mock.calls).toStrictEqual([
        [
          {
            projectIdOrKey: 'P',
            request: {
              name: 'Plan tests',
              parentId: 's2',
              typeId: 1,
              statusId: 1,
              priorityId: 2,
              progress: 0,
            },
          },
        ],
      ])
      expect(mockPlaceTask).not.toHaveBeenCalled()
      expect(mockMessage.success).not.toHaveBeenCalled()
    })

    it('waits for a milestone draft’s date before creating it', async () => {
      // Arrange
      renderPlan()
      await addChild(ROW.s2, 'Add Task')
      press('Tab') // Name left empty; on to Type
      await pick('Milestone')
      const draftRow = bodyCells('name').length - 1
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('Sign off')
      press('Enter') // a named draft opens the cell it still needs
      await flush()
      const callsBeforeDate = mockCreateTask.mock.calls.length
      const dateOpened = cellAt(draftRow, 'plannedStart').contains(openEditor())

      // Act
      typeDate('2026-05-01')
      press('Enter')
      await flush()

      // Assert
      expect(callsBeforeDate).toBe(0)
      expect(dateOpened).toBe(true)
      expect(mockCreateTask.mock.calls[0][0].request).toStrictEqual({
        name: 'Sign off',
        parentId: 's2',
        typeId: 2,
        statusId: 1,
        priorityId: 2,
        plannedDate: '2026-05-01',
      })
    })

    it('keeps a date entered before the draft became a milestone', async () => {
      // Arrange
      renderPlan()
      await addChild(ROW.s2, 'Add Task')
      const draftRow = bodyCells('name').length - 1
      editDate(draftRow, 'plannedStart', '2026-05-01')
      press('Enter')
      focus(cellAt(draftRow, 'type'))
      press('Enter')
      await pick('Milestone')
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('Sign off')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(mockCreateTask.mock.calls[0][0].request).toStrictEqual({
        name: 'Sign off',
        parentId: 's2',
        typeId: 2,
        statusId: 1,
        priorityId: 2,
        plannedDate: '2026-05-01',
      })
    })

    it('saves a value chosen while the task was being created', async () => {
      // Arrange
      let finishCreate!: () => void
      mockCreateTask.mockReturnValue({
        unwrap: () =>
          new Promise((resolve) => {
            finishCreate = () => resolve({ id: 'new-1', key: 'P-9' })
          }),
      })
      renderPlan()
      await addChild(ROW.s2, 'Add Task')
      typeInEditor('Plan tests')
      press('Tab') // the create goes out; Type opens on the draft
      press('Tab', { keyCode: 9, which: 9 }) // on to Status
      await pick('In Progress')

      // Act
      await act(async () => finishCreate())
      await flush()

      // Assert
      expect(mockPatchTask.mock.calls).toStrictEqual([
        [
          {
            projectIdOrKey: 'P',
            taskId: 'new-1',
            cacheKey: 'new-1',
            patchOperations: [{ op: 'replace', path: '/StatusId', value: 2 }],
          },
        ],
      ])
    })

    it('marks the milestone’s missing date', async () => {
      // Arrange
      renderPlan()
      await addChild(ROW.s2, 'Add Task')
      press('Tab')
      await pick('Milestone')
      const draftRow = bodyCells('name').length - 1
      focus(cellAt(draftRow, 'name'))
      press('Enter')
      typeInEditor('Sign off')

      // Act
      press('Tab')
      await flush()

      // Assert
      expect(cellAt(draftRow, 'plannedStart')).toHaveAccessibleDescription(
        'Planned Start: Required to create the item.',
      )
    })

    it('discards an empty draft with Escape', async () => {
      // Arrange
      renderPlan()
      const rows = bodyCells('name').length
      await addChild(ROW.s2, 'Add Task')

      // Act
      press('Escape')

      // Assert
      expect(bodyCells('name')).toHaveLength(rows)
      expect(mockCreateTask).not.toHaveBeenCalled()
    })
  })

  describe('row-action keys', () => {
    it('Insert adds a task below the row and creates it there', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t1, 'name'))
      press('Insert')
      const draftBelow = cellAt(ROW.t1 + 1, 'name').contains(openEditor())
      typeInEditor('Review')

      // Act
      press('Enter')
      await flush()

      // Assert
      expect(draftBelow).toBe(true)
      expect(mockCreateTask.mock.calls[0][0].request).toStrictEqual({
        name: 'Review',
        parentId: 's1',
        typeId: 1,
        statusId: 1,
        priorityId: 2,
        progress: 0,
        order: 2,
      })
      // Created in place, so nothing moves it afterwards
      expect(mockPlaceTask).not.toHaveBeenCalled()
    })

    it('Ctrl+Enter adds a task below too', () => {
      // Arrange
      renderPlan()
      const rows = bodyCells('name').length
      focus(cellAt(ROW.m1, 'status'))

      // Act
      press('Enter', { ctrlKey: true })

      // Assert
      expect(bodyCells('name')).toHaveLength(rows + 1)
      expect(cellAt(ROW.m1 + 1, 'name')).toContainElement(openEditor())
    })

    it('Insert on a stage adds nothing, since tasks go inside stages', () => {
      // Arrange
      renderPlan()
      const rows = bodyCells('name').length
      focus(cellAt(ROW.s1, 'name'))

      // Act
      press('Insert')

      // Assert
      expect(bodyCells('name')).toHaveLength(rows)
    })

    it('Alt+Shift+Right indents a task under the one above', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t2, 'name'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert — Go live is a milestone, which cannot hold tasks
      expect(mockPlaceTask).not.toHaveBeenCalled()
      expect(mockMessage.warning).toHaveBeenCalledWith(
        'Milestones cannot have child tasks',
      )
    })

    it('Alt+Shift+Right indents a milestone under the task above', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.m1, 'name'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockPlaceTask.mock.calls[0][0]).toStrictEqual({
        projectIdOrKey: 'P',
        id: 'm1',
        request: { taskId: 'm1', parentId: 't1', order: 1 },
      })
    })

    it('Alt+Shift+Left will not take a task out of its stage', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t1, 'name'))

      // Act
      press('ArrowLeft', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockPlaceTask).not.toHaveBeenCalled()
      expect(mockMessage.warning).toHaveBeenCalledWith(
        'Tasks must be placed within a stage',
      )
    })

    it('Ctrl+Delete asks to delete the task', () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t1, 'status'))

      // Act
      press('Delete', { ctrlKey: true })

      // Assert
      expect(
        screen.getByRole('dialog', { name: 'Delete task' }),
      ).toHaveTextContent('t1')
    })

    it('moves focus to the row taking a deleted task’s place', async () => {
      // Arrange
      const refetch = vi.fn(() => Promise.resolve())
      const props = {
        projectId: 'p-id',
        projectKey: 'P',
        isLoading: false,
        canManageTasks: true,
        refetch,
      }
      const { rerender } = render(<ProjectPlanTable {...props} tasks={PLAN} />)
      focus(cellAt(ROW.t1, 'status'))
      press('Delete', { ctrlKey: true })

      // Act
      fireEvent.click(screen.getByRole('button', { name: 'Confirm delete' }))
      const withoutDesign = [
        { ...PLAN[0], children: PLAN[0].children.slice(1) },
        PLAN[1],
      ]
      rerender(<ProjectPlanTable {...props} tasks={withoutDesign} />)
      await flush()

      // Assert
      expect(document.activeElement).toBe(cellAt(1, 'name'))
      expect(cellAt(1, 'name')).toHaveTextContent('Go live')
    })

    it('returns focus to the task when the delete is cancelled', async () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.t1, 'status'))
      press('Delete', { ctrlKey: true })

      // Act
      fireEvent.click(screen.getByRole('button', { name: 'Cancel delete' }))
      await flush()

      // Assert
      expect(document.activeElement).toBe(cellAt(ROW.t1, 'name'))
    })

    it('lists no drag gestures for a reader', () => {
      // Arrange
      renderPlan(false)

      // Act
      fireEvent.click(screen.getByRole('button', { name: /help/i }))

      // Assert
      expect(screen.queryByText('Drag and Drop')).not.toBeInTheDocument()
    })

    it('reports a move the server refuses', async () => {
      // Arrange
      mockPlaceTask.mockReturnValue(
        rejected({ status: 400, data: { detail: 'Not allowed.' } }),
      )
      renderPlan()
      focus(cellAt(ROW.m1, 'name'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockMessage.warning).toHaveBeenCalledWith('Not allowed.')
      expect(
        screen.getAllByRole('status').map((r) => r.textContent?.trim()),
      ).not.toContain('Indented.')
    })

    it('reports a move the server refuses with its own message', async () => {
      // Arrange — the placement call rejects with the problem body itself
      mockPlaceTask.mockReturnValue(rejected({ status: 400, detail: 'Raw.' }))
      renderPlan()
      focus(cellAt(ROW.m1, 'name'))

      // Act
      press('ArrowRight', { altKey: true, shiftKey: true })
      await flush()

      // Assert
      expect(mockMessage.warning).toHaveBeenCalledWith('Raw.')
    })

    it('Ctrl+Delete does nothing on a stage', () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.s1, 'status'))

      // Act
      press('Delete', { ctrlKey: true })

      // Assert
      expect(
        screen.queryByRole('dialog', { name: 'Delete task' }),
      ).not.toBeInTheDocument()
    })

    it('Ctrl+Shift+Enter opens the item drawer', () => {
      // Arrange
      renderPlan()
      focus(cellAt(ROW.m1, 'wbs'))

      // Act
      press('Enter', { ctrlKey: true, shiftKey: true })

      // Assert
      expect(
        screen.getByRole('dialog', { name: 'Plan item' }),
      ).toHaveTextContent('m1')
    })

    it('Ctrl+Shift+Enter opens the drawer for a reader too', () => {
      // Arrange
      renderPlan(false)
      focus(cellAt(ROW.t1, 'wbs'))

      // Act
      press('Enter', { ctrlKey: true, shiftKey: true })

      // Assert
      expect(screen.getByRole('dialog', { name: 'Plan item' })).toBeVisible()
    })

    it('gives a reader no keys that change the plan', async () => {
      // Arrange
      renderPlan(false)
      const rows = bodyCells('name').length
      focus(cellAt(ROW.m1, 'name'))

      // Act
      press('Insert')
      press('ArrowRight', { altKey: true, shiftKey: true })
      press('Delete', { ctrlKey: true })
      await flush()

      // Assert
      expect(bodyCells('name')).toHaveLength(rows)
      expect(mockPlaceTask).not.toHaveBeenCalled()
      expect(
        screen.queryByRole('dialog', { name: 'Delete task' }),
      ).not.toBeInTheDocument()
    })
  })

  describe('help', () => {
    it('lists the editing and row keys with the drag notes', () => {
      // Arrange
      renderPlan()

      // Act
      fireEvent.click(screen.getByRole('button', { name: /help/i }))

      // Assert
      expect(screen.getByText(/^Save and move down/)).toBeInTheDocument()
      expect(screen.getByText('Insert / Ctrl + Enter')).toBeInTheDocument()
      expect(screen.getByText('Ctrl + Shift + Enter')).toBeInTheDocument()
      expect(screen.getByText('Drag and Drop')).toBeInTheDocument()
    })

    it('lists only the keys a reader can use', () => {
      // Arrange
      renderPlan(false)

      // Act
      fireEvent.click(screen.getByRole('button', { name: /help/i }))

      // Assert
      expect(screen.queryByText(/^Save and move down/)).not.toBeInTheDocument()
      expect(screen.queryByText('Ctrl + Delete')).not.toBeInTheDocument()
      expect(screen.getByText('Ctrl + Shift + Enter')).toBeInTheDocument()
    })
  })
})
