import React from 'react'
import { render, screen } from '@testing-library/react'
import EmployeeWorkItems from './employee-work-items'
import { useGetEmployeeWorkItemsQuery } from '@/src/store/features/organizations/employee-api'
import { WorkStatusCategory } from '@/src/services/wayd-api'

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeWorkItemsQuery: jest.fn(),
}))

jest.mock('@/src/components/common/work', () => ({
  WorkItemsGrid: ({ workItems }: { workItems: { key: string }[] }) => (
    <div data-testid="work-items-grid">{workItems.length} items</div>
  ),
}))

const mockQuery = useGetEmployeeWorkItemsQuery as unknown as jest.Mock

describe('EmployeeWorkItems', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockQuery.mockReturnValue({
      data: [{ key: 'PLT-1' }, { key: 'PLT-2' }],
      isLoading: false,
      refetch: jest.fn(),
    })
  })

  it('renders the shared work items grid with the fetched items', () => {
    // Arrange / Act
    render(<EmployeeWorkItems employeeId="e1" />)

    // Assert
    expect(screen.getByTestId('work-items-grid')).toHaveTextContent('2 items')
  })

  it('requests only open work — Done and Removed are the cycle time report', () => {
    // Arrange / Act
    render(<EmployeeWorkItems employeeId="e1" />)

    // Assert
    expect(mockQuery).toHaveBeenCalledWith(
      {
        employeeId: 'e1',
        statusCategories: [
          WorkStatusCategory.Proposed,
          WorkStatusCategory.Active,
        ],
      },
      { skip: false },
    )
  })

  it('skips the query until an employee id is known', () => {
    // Arrange / Act
    render(<EmployeeWorkItems employeeId="" />)

    // Assert
    expect(mockQuery).toHaveBeenCalledWith(
      expect.objectContaining({ employeeId: '' }),
      { skip: true },
    )
  })
})
