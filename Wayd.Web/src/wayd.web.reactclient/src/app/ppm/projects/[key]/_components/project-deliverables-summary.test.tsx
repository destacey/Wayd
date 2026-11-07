import React from 'react'
import { render, screen } from '@testing-library/react'
import { ProjectDeliverableDto, StatusCategory } from '@/src/services/wayd-api'
import ProjectDeliverablesSummary from './project-deliverables-summary'

// The global stub replaces dayjs, and the target dates are formatted and compared through it.
jest.mock('dayjs', () => jest.requireActual('dayjs'))

jest.mock('@/src/utils/calendar-date', () => ({
  ...jest.requireActual('@/src/utils/calendar-date'),
  todayCalendarDate: () => '2026-11-06',
}))

const mockQuery = jest.fn()
jest.mock('@/src/store/features/ppm/project-deliverables-api', () => ({
  useGetProjectDeliverablesQuery: (projectId: string) => mockQuery(projectId),
}))

const deliverable = (
  name: string,
  category: StatusCategory,
  targetDate?: string,
): ProjectDeliverableDto =>
  ({
    id: name,
    projectId: 'p-1',
    name,
    order: 1,
    targetDate,
    status: {
      id: `s-${name}`,
      name: category,
      category,
      categoryLabel: category,
      alias: 0,
    },
    isOpen:
      category === StatusCategory.Proposed ||
      category === StatusCategory.Active,
  }) as ProjectDeliverableDto

describe('ProjectDeliverablesSummary', () => {
  beforeEach(() => {
    mockQuery.mockReset()
  })

  it('counts accepted against the deliverables still in scope and lists the open ones', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [
        deliverable('Pilot release', StatusCategory.Done),
        deliverable('Data migration', StatusCategory.Active, '2026-11-03'),
        deliverable(
          'Production release',
          StatusCategory.Proposed,
          '2027-01-15',
        ),
        deliverable('Training', StatusCategory.Removed),
      ],
    })

    // Act
    render(<ProjectDeliverablesSummary projectId="p-1" projectKey="CPM" />)

    // Assert
    expect(mockQuery).toHaveBeenCalledWith('p-1')
    expect(
      screen.getByLabelText('1 of 3 deliverables accepted'),
    ).toBeInTheDocument()
    expect(screen.getByText('Data migration')).toBeInTheDocument()
    expect(screen.getByText('Production release')).toBeInTheDocument()
    expect(screen.queryByText('Pilot release')).not.toBeInTheDocument()
    expect(screen.queryByText('Training')).not.toBeInTheDocument()
    expect(screen.getAllByText('Overdue')).toHaveLength(1)
    expect(screen.getByRole('link', { name: /View all/ })).toHaveAttribute(
      'href',
      '/ppm/projects/CPM?section=deliverables',
    )
  })

  it('lists only the next three open deliverables, soonest due first and undated last', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [
        deliverable('Undated', StatusCategory.Proposed),
        deliverable('Latest', StatusCategory.Proposed, '2027-03-01'),
        deliverable('Soonest', StatusCategory.Active, '2026-12-01'),
        deliverable('Middle', StatusCategory.Proposed, '2027-01-15'),
      ],
    })

    // Act
    render(<ProjectDeliverablesSummary projectId="p-1" projectKey="CPM" />)

    // Assert
    const names = ['Soonest', 'Middle', 'Latest', 'Undated'].filter((n) =>
      screen.queryByText(n),
    )
    expect(names).toEqual(['Soonest', 'Middle', 'Latest'])
    const soonest = screen.getByText('Soonest')
    const latest = screen.getByText('Latest')
    expect(
      soonest.compareDocumentPosition(latest) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
  })

  it('shows the linked work progress of an open deliverable that has any', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [
        {
          ...deliverable('Data migration', StatusCategory.Active, '2026-12-01'),
          progress: 42,
          linkedTaskCount: 3,
        },
        deliverable('Training', StatusCategory.Proposed, '2027-01-15'),
      ],
    })

    // Act
    render(<ProjectDeliverablesSummary projectId="p-1" projectKey="CPM" />)

    // Assert
    expect(
      screen.getByLabelText('42% of the linked plan work done'),
    ).toBeInTheDocument()
    expect(screen.getAllByRole('progressbar')).toHaveLength(2)
  })

  it('renders nothing when every deliverable was descoped', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [deliverable('Training', StatusCategory.Removed)],
    })

    // Act
    const { container } = render(
      <ProjectDeliverablesSummary projectId="p-1" projectKey="CPM" />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
