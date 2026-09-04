import React from 'react'
import { render, screen } from '@testing-library/react'
import ProjectTaskMetricsInline from './project-task-metrics-inline'
import { useGetProjectPlanSummaryQuery } from '@/src/store/features/ppm/projects-api'

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectPlanSummaryQuery: jest.fn(),
}))

const mockQuery = useGetProjectPlanSummaryQuery as unknown as jest.Mock

describe('ProjectTaskMetricsInline', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders each count with its label', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: { overdue: 3, dueThisWeek: 5, upcoming: 2, totalLeafTasks: 20 },
      isLoading: false,
    })

    // Act
    render(<ProjectTaskMetricsInline projectKey="PROJ-1" />)

    // Assert
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('overdue')).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
    expect(screen.getByText('due this week')).toBeInTheDocument()
    expect(screen.getByText('2')).toBeInTheDocument()
    expect(screen.getByText('upcoming')).toBeInTheDocument()
  })

  it('renders nothing while loading', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: undefined, isLoading: true })

    // Act
    const { container } = render(<ProjectTaskMetricsInline projectKey="P" />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when the project has no leaf tasks', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 0, upcoming: 0, totalLeafTasks: 0 },
      isLoading: false,
    })

    // Act
    const { container } = render(<ProjectTaskMetricsInline projectKey="P" />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('renders zero counts rather than hiding them', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 0, upcoming: 4, totalLeafTasks: 9 },
      isLoading: false,
    })

    // Act
    render(<ProjectTaskMetricsInline projectKey="P" />)

    // Assert
    expect(screen.getAllByText('0')).toHaveLength(2)
    expect(screen.getByText('overdue')).toBeInTheDocument()
  })

  it('passes the employee filter through to the query', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: { overdue: 1, dueThisWeek: 0, upcoming: 0, totalLeafTasks: 3 },
      isLoading: false,
    })

    // Act
    render(<ProjectTaskMetricsInline projectKey="PROJ-1" employeeId="emp-7" />)

    // Assert
    expect(mockQuery).toHaveBeenCalledWith({
      projectKey: 'PROJ-1',
      employeeId: 'emp-7',
    })
  })
})
