import React from 'react'
import { render, screen } from '@testing-library/react'
import ProjectStatusHistoryModal from './project-status-history-modal'
import { useGetProjectStatusHistoryQuery } from '@/src/store/features/ppm/projects-api'

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectStatusHistoryQuery: jest.fn(),
}))

jest.mock('@/src/components/common', () => ({
  LifecycleStatusTag: ({ status }: any) => <span>{status.name}</span>,
}))

jest.mock('antd', () => {
  const MockModal = ({ title, open, children }: any) =>
    open ? (
      <div>
        <div>{title}</div>
        {children}
      </div>
    ) : null
  MockModal.displayName = 'MockModal'

  const MockTimeline = ({ items }: any) => (
    <ul>
      {items.map((item: any, index: number) => (
        <li key={index} data-testid="timeline-item" data-color={item.color}>
          {item.content}
        </li>
      ))}
    </ul>
  )
  MockTimeline.displayName = 'MockTimeline'

  const MockEmpty = ({ description }: any) => <div>{description}</div>
  MockEmpty.displayName = 'MockEmpty'

  const MockSkeleton = ({ active }: any) =>
    active ? <div data-testid="skeleton" /> : null
  MockSkeleton.displayName = 'MockSkeleton'

  const MockTooltip = ({ children }: any) => <>{children}</>
  MockTooltip.displayName = 'MockTooltip'

  const MockFlex = ({ children }: any) => <div>{children}</div>
  MockFlex.displayName = 'MockFlex'

  const MockText = ({ children }: any) => <span>{children}</span>
  MockText.displayName = 'MockText'

  return {
    Modal: MockModal,
    Timeline: MockTimeline,
    Empty: MockEmpty,
    Skeleton: MockSkeleton,
    Tooltip: MockTooltip,
    Flex: MockFlex,
    Typography: { Text: MockText },
  }
})

const mockUseQuery = useGetProjectStatusHistoryQuery as jest.Mock

const entry = (overrides: Record<string, unknown> = {}) => ({
  id: 'entry-1',
  projectId: 'project-1',
  fromStatus: {
    id: 1,
    name: 'Proposed',
    lifecycleCategory: 'NotStarted',
  },
  toStatus: { id: 2, name: 'Active', lifecycleCategory: 'Active' },
  changedBy: { id: 'emp-1', key: 1, name: 'Dakota Reyes' },
  changedOn: new Date('2026-03-01T14:30:00Z'),
  source: { id: 1, name: 'Recorded' },
  reason: undefined,
  ...overrides,
})

describe('ProjectStatusHistoryModal', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders nothing when closed', () => {
    mockUseQuery.mockReturnValue({ data: undefined, isLoading: false })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={false}
        onClose={jest.fn()}
      />,
    )

    expect(screen.queryByText('Status History')).not.toBeInTheDocument()
  })

  it('skips the query while closed', () => {
    mockUseQuery.mockReturnValue({ data: undefined, isLoading: false })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={false}
        onClose={jest.fn()}
      />,
    )

    expect(mockUseQuery).toHaveBeenCalledWith('project-1', { skip: true })
  })

  it('shows a skeleton while loading', () => {
    mockUseQuery.mockReturnValue({ data: undefined, isLoading: true })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={true}
        onClose={jest.fn()}
      />,
    )

    expect(screen.getByTestId('skeleton')).toBeInTheDocument()
  })

  it('shows an empty state when there is no history', () => {
    mockUseQuery.mockReturnValue({ data: [], isLoading: false })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={true}
        onClose={jest.fn()}
      />,
    )

    expect(
      screen.getByText(
        'No status changes have been recorded for this project.',
      ),
    ).toBeInTheDocument()
  })

  it('renders a transition with both statuses and the changing employee', () => {
    mockUseQuery.mockReturnValue({ data: [entry()], isLoading: false })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={true}
        onClose={jest.fn()}
      />,
    )

    expect(screen.getByText('Proposed')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.getByText(/Dakota Reyes/)).toBeInTheDocument()
  })

  it('omits the from status when the project entered its initial state', () => {
    mockUseQuery.mockReturnValue({
      data: [
        entry({
          fromStatus: undefined,
          toStatus: {
            id: 1,
            name: 'Proposed',
            lifecycleCategory: 'NotStarted',
          },
        }),
      ],
      isLoading: false,
    })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={true}
        onClose={jest.fn()}
      />,
    )

    expect(screen.getByText('Proposed')).toBeInTheDocument()
    expect(screen.queryByText('→')).not.toBeInTheDocument()
  })

  it('attributes a recorded change with no employee to the system', () => {
    mockUseQuery.mockReturnValue({
      data: [entry({ changedBy: undefined })],
      isLoading: false,
    })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={true}
        onClose={jest.fn()}
      />,
    )

    expect(screen.getByText(/System/)).toBeInTheDocument()
  })

  it('attributes a reconstructed row with no employee to an unknown actor', () => {
    mockUseQuery.mockReturnValue({
      data: [
        entry({
          changedBy: undefined,
          source: { id: 2, name: 'Reconstructed' },
        }),
      ],
      isLoading: false,
    })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={true}
        onClose={jest.fn()}
      />,
    )

    expect(screen.getByText(/Unknown/)).toBeInTheDocument()
  })

  it('never surfaces the row source to the reader', () => {
    mockUseQuery.mockReturnValue({
      data: [
        entry(),
        entry({ id: 'entry-2', source: { id: 2, name: 'Reconstructed' } }),
        entry({ id: 'entry-3', source: { id: 3, name: 'Synthesized' } }),
      ],
      isLoading: false,
    })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={true}
        onClose={jest.fn()}
      />,
    )

    expect(screen.queryByText('Recorded')).not.toBeInTheDocument()
    expect(screen.queryByText('Reconstructed')).not.toBeInTheDocument()
    expect(screen.queryByText('Synthesized')).not.toBeInTheDocument()
  })

  it('renders the reason when one was given', () => {
    mockUseQuery.mockReturnValue({
      data: [entry({ reason: 'Funding secured' })],
      isLoading: false,
    })

    render(
      <ProjectStatusHistoryModal
        projectId="project-1"
        isOpen={true}
        onClose={jest.fn()}
      />,
    )

    expect(screen.getByText('Funding secured')).toBeInTheDocument()
  })
})
