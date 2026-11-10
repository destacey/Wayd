import React from 'react'
import { render, screen } from '@testing-library/react'
import { ProjectDeliverableDto, StatusCategory } from '@/src/services/wayd-api'
import ProjectDeliverablesSummary from './project-deliverables-summary'

vi.mock(import('@/src/utils/calendar-date'), async (importOriginal) => ({
  ...(await importOriginal()),
  todayCalendarDate: () => '2026-11-06',
}))

const mockQuery = vi.fn()
vi.mock('@/src/store/features/ppm/project-deliverables-api', () => ({
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

  it('lists the most recently accepted deliverables once none is open', () => {
    // Arrange
    const accepted = (name: string, acceptedOn: string) => ({
      ...deliverable(name, StatusCategory.Done),
      acceptedOn: new Date(acceptedOn),
    })
    mockQuery.mockReturnValue({
      data: [
        accepted('Pilot release', '2026-09-01T12:00:00Z'),
        accepted('Data migration', '2026-10-15T12:00:00Z'),
        accepted('Production release', '2026-11-02T12:00:00Z'),
        accepted('Runbook', '2026-10-20T12:00:00Z'),
        deliverable('Training', StatusCategory.Removed),
      ],
    })

    // Act
    render(<ProjectDeliverablesSummary projectId="p-1" projectKey="CPM" />)

    // Assert
    expect(
      screen.getByLabelText('4 of 4 deliverables accepted'),
    ).toBeInTheDocument()
    const names = [
      'Production release',
      'Runbook',
      'Data migration',
      'Pilot release',
      'Training',
    ].filter((n) => screen.queryByText(n))
    expect(names).toEqual(['Production release', 'Runbook', 'Data migration'])
    const latest = screen.getByText('Production release')
    const third = screen.getByText('Data migration')
    expect(
      latest.compareDocumentPosition(third) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
    expect(screen.getByText('Nov 2, 2026')).toBeInTheDocument()
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
