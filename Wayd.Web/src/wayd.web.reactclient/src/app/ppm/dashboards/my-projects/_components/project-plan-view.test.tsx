import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import ProjectPlanView from './project-plan-view'
import { ProjectPlanNodeDto } from '@/src/services/wayd-api'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectPlanTreeQuery: jest.fn(),
}))

import { useGetProjectPlanTreeQuery } from '@/src/store/features/ppm/projects-api'

const mockQuery = useGetProjectPlanTreeQuery as jest.Mock

function createNode(
  overrides: Partial<ProjectPlanNodeDto> & { name: string; nodeType: string },
): ProjectPlanNodeDto {
  return {
    id: `node-${Math.random()}`,
    status: { id: 1, name: 'Not Started' } as any,
    order: 1,
    wbs: '1',
    progress: 0,
    assignees: [],
    children: [],
    ...overrides,
  } as ProjectPlanNodeDto
}

function createStage(
  name: string,
  statusName: string,
  children: ProjectPlanNodeDto[] = [],
  progress: number = 0,
): ProjectPlanNodeDto {
  return createNode({
    name,
    nodeType: 'Stage',
    status: { id: 1, name: statusName } as any,
    children,
    progress,
  })
}

function createTask(
  name: string,
  statusName: string,
  overrides?: Partial<ProjectPlanNodeDto>,
): ProjectPlanNodeDto {
  return createNode({
    name,
    nodeType: 'Task',
    status: { id: 1, name: statusName } as any,
    ...overrides,
  })
}

describe('ProjectPlanView', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders loading skeleton when data is loading', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: true })

    const { container } = render(<ProjectPlanView projectKey="P1" />)

    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders empty message when no plan data', () => {
    mockQuery.mockReturnValue({ data: [], isLoading: false })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('No project plan defined.')).toBeInTheDocument()
  })

  it('renders stage names', () => {
    mockQuery.mockReturnValue({
      data: [
        createStage('Discovery', 'Completed'),
        createStage('Design', 'In Progress'),
        createStage('Build', 'Not Started'),
      ],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('Discovery')).toBeInTheDocument()
    expect(screen.getByText('Design')).toBeInTheDocument()
    expect(screen.getByText('Build')).toBeInTheDocument()
  })

  it('renders stage status tags', () => {
    mockQuery.mockReturnValue({
      data: [
        createStage('Discovery', 'Completed'),
        createStage('Design', 'In Progress'),
      ],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('Completed')).toBeInTheDocument()
    expect(screen.getByText('In Progress')).toBeInTheDocument()
  })

  it('renders progress bar for stages', () => {
    mockQuery.mockReturnValue({
      data: [createStage('Discovery', 'In Progress', [], 75)],
      isLoading: false,
    })

    const { container } = render(<ProjectPlanView projectKey="P1" />)

    expect(container.querySelector('.ant-progress')).toBeInTheDocument()
  })

  it('expands active stage by default', () => {
    const task = createTask('Task 1', 'Not Started')
    mockQuery.mockReturnValue({
      data: [createStage('Active Stage', 'In Progress', [task])],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('Task 1')).toBeInTheDocument()
  })

  it('collapses non-active stages by default', () => {
    const task = createTask('Hidden Task', 'Not Started')
    mockQuery.mockReturnValue({
      data: [createStage('Future Stage', 'Not Started', [task])],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.queryByText('Hidden Task')).not.toBeInTheDocument()
  })

  it('toggles stage open/closed on click', async () => {
    const task = createTask('Toggle Task', 'Not Started')
    mockQuery.mockReturnValue({
      data: [createStage('Collapsible', 'Not Started', [task])],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.queryByText('Toggle Task')).not.toBeInTheDocument()

    await userEvent.click(screen.getByText('Collapsible'))

    expect(screen.getByText('Toggle Task')).toBeInTheDocument()

    await userEvent.click(screen.getByText('Collapsible'))

    expect(screen.queryByText('Toggle Task')).not.toBeInTheDocument()
  })

  it('renders task with completed icon styling', () => {
    const task = createTask('Done Task', 'Completed')
    mockQuery.mockReturnValue({
      data: [createStage('Stage', 'In Progress', [task])],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('Done Task')).toBeInTheDocument()
  })

  it('does not show stat pills when tasks have no dates', () => {
    const task = createTask('No Date', 'Not Started')
    mockQuery.mockReturnValue({
      data: [createStage('Stage', 'In Progress', [task])],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.queryByText(/overdue/)).not.toBeInTheDocument()
    expect(screen.queryByText(/upcoming/)).not.toBeInTheDocument()
  })

  it('renders deliverable sections with task count', async () => {
    const deliverable = createNode({
      name: 'Model Migration',
      nodeType: 'Task',
      children: [
        createTask('Sub Task 1', 'Completed'),
        createTask('Sub Task 2', 'Not Started'),
      ],
    })
    mockQuery.mockReturnValue({
      data: [createStage('Stage', 'In Progress', [deliverable])],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('Model Migration')).toBeInTheDocument()
    expect(screen.getByText('1/2')).toBeInTheDocument()
  })

  it('renders completed task with Complete badge', () => {
    const task = createTask('Done Task', 'Completed')
    mockQuery.mockReturnValue({
      data: [createStage('Stage', 'In Progress', [task])],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('Complete')).toBeInTheDocument()
  })

  it('renders task assignee avatars', () => {
    const task = createTask('Assigned Task', 'In Progress', {
      assignees: [{ id: 'emp-1', key: 1, name: 'Alice Brown' }] as any[],
    })
    mockQuery.mockReturnValue({
      data: [createStage('Stage', 'In Progress', [task])],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('AB')).toBeInTheDocument()
  })

  it('only renders Stage nodes at root level', () => {
    mockQuery.mockReturnValue({
      data: [
        createStage('Real Stage', 'In Progress'),
        createNode({ name: 'Stray Task', nodeType: 'Task' }),
      ],
      isLoading: false,
    })

    render(<ProjectPlanView projectKey="P1" />)

    expect(screen.getByText('Real Stage')).toBeInTheDocument()
    expect(screen.queryByText('Stray Task')).not.toBeInTheDocument()
  })
})
