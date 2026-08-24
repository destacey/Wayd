import React from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import ProjectPlanItemDrawer from './project-plan-item-drawer'

jest.mock('@/src/store/features/ppm/project-tasks-api', () => ({
  useGetProjectTaskQuery: jest.fn(),
}))

jest.mock('@/src/components/common/content', () => ({
  LabeledContent: ({
    label,
    children,
  }: {
    label: React.ReactNode
    children: React.ReactNode
  }) => (
    <div>
      <div>{label}</div>
      <div>{children}</div>
    </div>
  ),
}))

jest.mock('@/src/components/common/markdown', () => ({
  MarkdownRenderer: ({ markdown }: { markdown: string }) => (
    <div data-testid="markdown">{markdown}</div>
  ),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    error: jest.fn(),
    warning: jest.fn(),
    success: jest.fn(),
    info: jest.fn(),
  }),
}))

jest.mock('next/link', () => {
  const MockLink = ({ href, children }: any) => <a href={href}>{children}</a>
  MockLink.displayName = 'MockLink'
  return MockLink
})

jest.mock('antd', () => {
  const MockDrawer = ({ title, open, children, extra }: any) =>
    open ? (
      <div>
        <div>{title}</div>
        <div>{extra}</div>
        {children}
      </div>
    ) : null
  MockDrawer.displayName = 'MockDrawer'

  const MockButton = ({ children, onClick }: any) => (
    <button onClick={onClick}>{children}</button>
  )
  MockButton.displayName = 'MockButton'

  const MockDropdown = ({ menu, children }: any) => (
    <div>
      {children}
      <div>
        {(menu?.items ?? [])
          .filter((item: any) => item?.type !== 'divider')
          .map((item: any) => (
            <button key={item.key} onClick={item.onClick}>
              {item.label}
            </button>
          ))}
      </div>
    </div>
  )
  MockDropdown.displayName = 'MockDropdown'

  const MockDivider = () => <hr />
  MockDivider.displayName = 'MockDivider'

  const MockFlex = ({ children }: any) => <div>{children}</div>
  MockFlex.displayName = 'MockFlex'

  return {
    Drawer: MockDrawer,
    Button: MockButton,
    Dropdown: MockDropdown,
    Divider: MockDivider,
    Flex: MockFlex,
  }
})

const { useGetProjectTaskQuery } = jest.requireMock(
  '@/src/store/features/ppm/project-tasks-api',
)

describe('ProjectPlanItemDrawer', () => {
  const baseProps = {
    projectKey: 'PROJ-1',
    taskId: 'task-1',
    stageName: 'Build',
    drawerOpen: true,
    onDrawerClose: jest.fn(),
    onOpenTask: jest.fn(),
    onEditTask: jest.fn(),
    onDeleteTask: jest.fn(),
    onAddChildTask: jest.fn(),
  }

  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('hides estimated effort when not provided and hides parent task when absent', () => {
    useGetProjectTaskQuery.mockReturnValue({
      data: {
        id: 'task-1',
        key: 'TASK-1',
        name: 'Task 1',
        type: { name: 'Task' },
        status: { name: 'In Progress' },
        priority: { name: 'Medium' },
        progress: 50,
        assignees: [],
        description: 'desc',
      },
      isLoading: false,
      error: undefined,
    })

    render(<ProjectPlanItemDrawer {...baseProps} />)

    expect(screen.queryByText('Estimated Effort (hrs)')).not.toBeInTheDocument()
    expect(screen.queryByText('Parent Task')).not.toBeInTheDocument()
  })

  it('renders assignees as linked list and description as markdown', () => {
    useGetProjectTaskQuery.mockReturnValue({
      data: {
        id: 'task-1',
        key: 'TASK-1',
        name: 'Task 1',
        type: { name: 'Task' },
        status: { name: 'In Progress' },
        priority: { name: 'Medium' },
        progress: 50,
        assignees: [
          { id: 'e1', key: 'EMP-1', name: 'Alice' },
          { id: 'e2', key: 'EMP-2', name: 'Bob' },
        ],
        description: '**markdown** text',
      },
      isLoading: false,
      error: undefined,
    })

    render(<ProjectPlanItemDrawer {...baseProps} />)

    expect(screen.getByRole('link', { name: 'Alice' })).toHaveAttribute(
      'href',
      '/organizations/employees/EMP-1',
    )
    expect(screen.getByRole('link', { name: 'Bob' })).toHaveAttribute(
      'href',
      '/organizations/employees/EMP-2',
    )
    expect(screen.getByTestId('markdown')).toHaveTextContent('**markdown** text')
  })

  it('fires row menu actions', () => {
    useGetProjectTaskQuery.mockReturnValue({
      data: {
        id: 'task-1',
        key: 'TASK-1',
        name: 'Task 1',
        type: { name: 'Task' },
        status: { name: 'In Progress' },
        priority: { name: 'Medium' },
        progress: 50,
        assignees: [],
        description: '',
      },
      isLoading: false,
      error: undefined,
    })

    render(<ProjectPlanItemDrawer {...baseProps} />)

    fireEvent.click(screen.getByRole('button', { name: 'Edit' }))
    fireEvent.click(screen.getByRole('button', { name: 'Add Child Task' }))
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))

    expect(baseProps.onEditTask).toHaveBeenCalledWith('task-1')
    expect(baseProps.onAddChildTask).toHaveBeenCalledWith('task-1')
    expect(baseProps.onDeleteTask).toHaveBeenCalledWith('task-1')
  })
})
