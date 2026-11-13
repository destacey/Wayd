import type { Mock } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import MyProjectTasksWidget, {
  countTasksDue,
  DueThisWeekTile,
  endOfWeek,
  MY_PROJECT_TASKS_LIMIT,
  OverdueTasksTile,
  taskDue,
  tasksDueSoon,
} from './my-project-tasks-widget'
import {
  HomeContextResponse,
  MyProjectTaskDto,
  ProjectStatus,
} from '@/src/services/wayd-api'
import { tileValueStyle } from '../attention-tile-test-utils'
import { homeContext } from '../home-context-test-utils'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetMyProjectTasksQuery: vi.fn(),
}))

// A Wednesday: the week ends on Saturday 2026-11-14.
const TODAY = '2026-11-11'

vi.mock(import('@/src/utils'), async (importOriginal) => ({
  ...(await importOriginal()),
  todayCalendarDate: () => TODAY,
}))

import { useGetMyProjectTasksQuery } from '@/src/store/features/ppm/projects-api'

const mockTasks = useGetMyProjectTasksQuery as Mock

const task = (
  number: number,
  name: string,
  dueDate?: string,
  projectKey = 'APOLLO',
): MyProjectTaskDto => ({
  id: `task-${projectKey}-${number}`,
  key: `${projectKey}-${number}`,
  name,
  type: { id: 1, name: 'Task' },
  status: { id: 2, name: 'In Progress' },
  priority: { id: 2, name: 'Medium' },
  dueDate,
  project: { id: `project-${projectKey}`, key: projectKey, name: 'Apollo' },
})

const loaded = (tasks: MyProjectTaskDto[], isError = false) => ({
  data: tasks,
  isLoading: false,
  isError,
})

beforeEach(() => {
  vi.clearAllMocks()
  mockHome.context = undefined
})

describe('endOfWeek', () => {
  it.each([
    ['a Wednesday', '2026-11-11', '2026-11-14'],
    ['a Saturday', '2026-11-14', '2026-11-14'],
    ['a Sunday', '2026-11-15', '2026-11-21'],
  ])('ends the week of %s on its Saturday', (_, today, saturday) => {
    // Arrange / Act
    const end = endOfWeek(today)

    // Assert
    expect(end).toBe(saturday)
  })
})

describe('taskDue', () => {
  it.each([
    ['2026-11-10', 'overdue'],
    ['2026-11-11', 'this-week'],
    ['2026-11-14', 'this-week'],
    ['2026-11-15', 'later'],
    [undefined, 'none'],
  ])('places a task due %s as %s', (dueDate, expected) => {
    // Arrange
    const t = task(1, 'Design', dueDate)

    // Act
    const due = taskDue(t, TODAY)

    // Assert
    expect(due).toBe(expected)
  })
})

describe('tasksDueSoon', () => {
  it('keeps overdue tasks and those due by the end of next week, counting the rest', () => {
    // Arrange
    const tasks = [
      task(1, 'Late', '2026-10-01'),
      task(2, 'Saturday next week', '2026-11-21'),
      task(3, 'Sunday after', '2026-11-22'),
      task(4, 'Undated'),
    ]

    // Act
    const { soon, laterCount } = tasksDueSoon(tasks, TODAY)

    // Assert
    expect(soon.map((t) => t.name)).toEqual(['Late', 'Saturday next week'])
    expect(laterCount).toBe(2)
  })
})

describe('countTasksDue', () => {
  it('counts overdue and due-this-week tasks apart, leaving later and undated ones out', () => {
    // Arrange
    const tasks = [
      task(1, 'Late', '2026-11-01'),
      task(2, 'Late too', '2026-11-10'),
      task(3, 'Today', '2026-11-11'),
      task(4, 'Next week', '2026-11-16'),
      task(5, 'Undated'),
    ]

    // Act
    const counts = countTasksDue(tasks, TODAY)

    // Assert
    expect(counts).toEqual({ overdue: 2, dueThisWeek: 1 })
  })
})

describe('MyProjectTasksWidget', () => {
  it('asks for tasks on approved and active projects', () => {
    // Arrange
    mockTasks.mockReturnValue(loaded([]))

    // Act
    render(<MyProjectTasksWidget title="My project tasks" />)

    // Assert
    expect(mockTasks).toHaveBeenCalledWith(
      { status: [ProjectStatus.Approved, ProjectStatus.Active] },
      { skip: false },
    )
  })

  it('skips the request when the home context says there are no projects', () => {
    // Arrange
    mockHome.context = homeContext()
    mockTasks.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<MyProjectTasksWidget title="My project tasks" />)

    // Assert
    expect(mockTasks).toHaveBeenCalledWith(expect.anything(), { skip: true })
    expect(
      screen.getByText(/You have no open project tasks/),
    ).toBeInTheDocument()
  })

  it('lists each task due soon with its due date and project, linking to the plan', () => {
    // Arrange
    mockTasks.mockReturnValue(
      loaded([
        task(3, 'Design', '2026-11-09'),
        task(7, 'Build', '2026-11-13'),
        task(8, 'Rollout', '2026-12-01'),
        task(9, 'Release'),
      ]),
    )

    // Act
    render(<MyProjectTasksWidget title="My project tasks" />)

    // Assert
    const items = screen.getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(
      within(items[0]).getByRole('link', { name: 'APOLLO-3 · Design' }),
    ).toHaveAttribute('href', '/ppm/projects/APOLLO?section=plan')
    expect(within(items[0]).getByText(/\(overdue\)/)).toBeInTheDocument()
    expect(
      within(items[0]).getByRole('link', { name: 'Apollo' }),
    ).toHaveAttribute('href', '/ppm/projects/APOLLO')
    expect(within(items[1]).queryByText(/overdue/)).not.toBeInTheDocument()
    expect(
      screen.getByText('You have 2 more open tasks due later.'),
    ).toBeInTheDocument()
  })

  it('says nothing is due soon, and how many are due later, when every task is later', () => {
    // Arrange
    mockTasks.mockReturnValue(loaded([task(1, 'Rollout', '2026-12-01')]))

    // Act
    render(<MyProjectTasksWidget title="My project tasks" />)

    // Assert
    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
    expect(
      screen.getByText(
        'Nothing is due by the end of next week. You have 1 open task due later.',
      ),
    ).toBeInTheDocument()
  })

  it('lists the first tasks and counts the rest', () => {
    // Arrange
    const tasks = Array.from({ length: MY_PROJECT_TASKS_LIMIT + 3 }, (_, i) =>
      task(i + 1, `Task ${i + 1}`, '2026-11-20'),
    )
    mockTasks.mockReturnValue(loaded(tasks))

    // Act
    render(<MyProjectTasksWidget title="My project tasks" />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(MY_PROJECT_TASKS_LIMIT)
    expect(
      screen.getByText('And 3 more due by the end of next week.'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/due later/)).not.toBeInTheDocument()
  })

  it('lists as many tasks as its rows setting says', () => {
    // Arrange
    const tasks = Array.from({ length: 5 }, (_, i) =>
      task(i + 1, `Task ${i + 1}`, '2026-11-20'),
    )
    mockTasks.mockReturnValue(loaded(tasks))

    // Act
    render(
      <MyProjectTasksWidget
        title="My project tasks"
        settings={{ rows: '3' }}
      />,
    )

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
    expect(
      screen.getByText('And 2 more due by the end of next week.'),
    ).toBeInTheDocument()
  })

  it('says the tasks failed to load rather than that there are none', () => {
    // Arrange
    mockTasks.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<MyProjectTasksWidget title="My project tasks" />)

    // Assert
    expect(screen.getByText('Could not load your tasks.')).toBeInTheDocument()
    expect(
      screen.queryByText(/You have no open project tasks/),
    ).not.toBeInTheDocument()
  })
})

describe('OverdueTasksTile', () => {
  it('counts overdue tasks in the error colour, linking to the widget', () => {
    // Arrange
    mockTasks.mockReturnValue(
      loaded([task(1, 'Late', '2026-11-01'), task(2, 'Today', '2026-11-11')]),
    )

    // Act
    const { container } = render(<OverdueTasksTile />)

    // Assert
    const tile = screen.getByRole('button', {
      name: 'Overdue tasks assigned to you: 1',
    })
    expect(tile).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-error)')
  })

  it('counts zero without asking when the home context says there are no projects', () => {
    // Arrange
    mockHome.context = homeContext()
    mockTasks.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<OverdueTasksTile />)

    // Assert
    expect(mockTasks).toHaveBeenCalledWith(expect.anything(), { skip: true })
    expect(
      screen.getByRole('button', { name: 'Overdue tasks assigned to you: 0' }),
    ).toBeInTheDocument()
  })

  it('shows a dash, not the last good count, after a failed refetch', () => {
    // Arrange
    mockTasks.mockReturnValue(loaded([task(1, 'Late', '2026-11-01')], true))

    // Act
    render(<OverdueTasksTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Overdue tasks assigned to you: unavailable',
      }),
    ).toBeInTheDocument()
  })
})

describe('DueThisWeekTile', () => {
  it('counts tasks due by Saturday in the warning colour', () => {
    // Arrange
    mockTasks.mockReturnValue(
      loaded([
        task(1, 'Late', '2026-11-01'),
        task(2, 'Today', '2026-11-11'),
        task(3, 'Saturday', '2026-11-14'),
        task(4, 'Sunday', '2026-11-15'),
      ]),
    )

    // Act
    const { container } = render(<DueThisWeekTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Due this week assigned to you: 2' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-warning)')
  })
})
