import React from 'react'
import { render, screen } from '@testing-library/react'
import {
  IntakeRequestOutcomeKind,
  ProjectIntakeRequestDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import ProjectIntakeRequests from './project-intake-requests'

const mockQuery = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetProjectIntakeRequestsQuery: (projectId: string) =>
      mockQuery(projectId),
  }),
)

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
  } as ProjectIntakeRequestDto,
]

describe('ProjectIntakeRequests', () => {
  beforeEach(() => {
    mockQuery.mockReset()
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
