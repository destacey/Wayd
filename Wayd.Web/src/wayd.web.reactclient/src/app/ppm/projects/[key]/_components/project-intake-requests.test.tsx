import React from 'react'
import { render, screen } from '@testing-library/react'
import {
  IntakeRequestOutcomeKind,
  ProjectIntakeRequestDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import ProjectIntakeRequests from './project-intake-requests'

const mockQuery = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useGetProjectIntakeRequestsQuery: (projectId: string) => mockQuery(projectId),
}))

const mockDeliverables = vi.fn()
vi.mock('@/src/store/features/ppm/project-deliverables-api', () => ({
  useGetProjectDeliverablesQuery: (projectId: string) =>
    mockDeliverables(projectId),
}))

const requests: ProjectIntakeRequestDto[] = [
  {
    request: { id: 'r-7', key: 7, title: 'Export invoices' },
    status: {
      id: 's-1',
      name: 'Accepted',
      category: StatusCategory.Active,
      alias: 4,
    },
    kind: IntakeRequestOutcomeKind.ExistingProjectScope,
    note: 'The export screen',
    deliverableIds: [] as string[],
  } as ProjectIntakeRequestDto,
]

describe('ProjectIntakeRequests', () => {
  beforeEach(() => {
    mockQuery.mockReset()
    mockDeliverables.mockReset()
    mockDeliverables.mockReturnValue({ data: [] })
  })

  it('names the deliverables a request waits on, by their names on the project', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [{ ...requests[0], deliverableIds: ['d-2', 'd-1'] }],
      isLoading: false,
    })
    mockDeliverables.mockReturnValue({
      data: [
        { id: 'd-1', name: 'Customer portal' },
        { id: 'd-2', name: 'Reports' },
        { id: 'd-3', name: 'Archive' },
      ],
    })

    // Act
    render(<ProjectIntakeRequests projectId="p-1" />)

    // Assert
    expect(mockDeliverables).toHaveBeenCalledWith('p-1')
    expect(
      screen.getByText('Waits on Reports, Customer portal'),
    ).toBeInTheDocument()
  })

  it('counts mapped deliverables since deleted, and says nothing of them until the names load', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: [{ ...requests[0], deliverableIds: ['d-1', 'd-gone'] }],
      isLoading: false,
    })
    mockDeliverables.mockReturnValue({ data: undefined })

    // Act
    const { rerender } = render(<ProjectIntakeRequests projectId="p-1" />)

    // Assert
    expect(screen.queryByText(/Waits on/)).toBeNull()
    mockDeliverables.mockReturnValue({
      data: [{ id: 'd-1', name: 'Customer portal' }],
    })
    rerender(<ProjectIntakeRequests projectId="p-1" />)
    expect(
      screen.getByText('Waits on Customer portal, 1 deliverable since deleted'),
    ).toBeInTheDocument()
  })

  it('lists the requests behind the project, linking each', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: requests, isLoading: false })

    // Act
    render(<ProjectIntakeRequests projectId="p-1" />)

    // Assert
    expect(mockQuery).toHaveBeenCalledWith('p-1')
    expect(
      screen.getByRole('link', { name: '7 · Export invoices' }),
    ).toHaveAttribute('href', '/demand-management/requests/7')
    expect(screen.getByText('Accepted')).toBeInTheDocument()
    expect(screen.getByText('Existing project scope')).toBeInTheDocument()
    expect(screen.getByText('The export screen')).toBeInTheDocument()
  })

  it('says none is linked without counting the ones the reader cannot see', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: [], isLoading: false })

    // Act
    render(<ProjectIntakeRequests projectId="p-1" />)

    // Assert
    expect(
      screen.getByText('No request you can see is linked to this project.'),
    ).toBeInTheDocument()
  })
})
