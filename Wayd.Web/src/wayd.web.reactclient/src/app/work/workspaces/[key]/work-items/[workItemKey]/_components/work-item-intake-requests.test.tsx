import React from 'react'
import { render, screen } from '@testing-library/react'
import {
  StatusCategory,
  WorkItemIntakeRequestDto,
} from '@/src/services/wayd-api'
import WorkItemIntakeRequests from './work-item-intake-requests'

const mockQuery = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetWorkItemIntakeRequestsQuery: (workItemId: string) =>
      mockQuery(workItemId),
  }),
)

const requests: WorkItemIntakeRequestDto[] = [
  {
    request: { id: 'r-7', key: 7, title: 'Export invoices' },
    status: {
      id: 's-1',
      name: 'Accepted',
      category: StatusCategory.Active,
      alias: 4,
    },
    note: 'The export screen',
  } as WorkItemIntakeRequestDto,
]

describe('WorkItemIntakeRequests', () => {
  beforeEach(() => {
    mockQuery.mockReset()
  })

  it('lists the requests the work item delivers, linking each', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: requests, isLoading: false })

    // Act
    render(<WorkItemIntakeRequests workItemId="w-1" />)

    // Assert
    expect(mockQuery).toHaveBeenCalledWith('w-1')
    expect(
      screen.getByRole('link', { name: '7 · Export invoices' }),
    ).toHaveAttribute('href', '/demand-management/requests/7')
    expect(screen.getByText('Accepted')).toBeInTheDocument()
    expect(screen.getByText('The export screen')).toBeInTheDocument()
  })

  it('says none is linked without counting the ones the reader cannot see', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: [], isLoading: false })

    // Act
    render(<WorkItemIntakeRequests workItemId="w-1" />)

    // Assert
    expect(
      screen.getByText('No request you can see is linked to this work item.'),
    ).toBeInTheDocument()
  })

  it('offers a retry when the requests cannot be loaded', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: { status: 500 },
      refetch: jest.fn(),
    })

    // Act
    render(<WorkItemIntakeRequests workItemId="w-1" />)

    // Assert
    expect(
      screen.getByText('The requests could not be loaded.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Try again' }),
    ).toBeInTheDocument()
  })
})
