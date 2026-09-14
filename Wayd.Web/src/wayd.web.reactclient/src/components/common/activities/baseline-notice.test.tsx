import { render, screen } from '@testing-library/react'
import BaselineNotice from './baseline-notice'

const mockEmployee = jest.fn()

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeByIdQuery: (...args: unknown[]) => mockEmployee(...args),
}))

describe('BaselineNotice', () => {
  beforeEach(() => {
    mockEmployee.mockReset()
    mockEmployee.mockReturnValue({
      data: { id: 'emp-1', key: 42, displayName: 'Jordan Rivera' },
    })
  })

  it('shows when the record was created and who created it', () => {
    render(
      <BaselineNotice
        payload={JSON.stringify({
          recordCreatedOn: '2024-03-04T15:30:00Z',
          recordCreatedById: 'emp-1',
        })}
      />,
    )

    expect(screen.getByText('Tracking started')).toBeInTheDocument()
    expect(screen.getByText(/Record created Mar 4, 2024/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Jordan Rivera' })).toHaveAttribute(
      'href',
      '/organizations/employees/42',
    )
    expect(mockEmployee).toHaveBeenCalledWith('emp-1')
  })

  it('shows the creation date alone when the creator was not recorded', () => {
    render(
      <BaselineNotice
        payload={JSON.stringify({ recordCreatedOn: '2024-03-04T15:30:00Z' })}
      />,
    )

    expect(screen.getByText('Record created Mar 4, 2024')).toBeInTheDocument()
    expect(mockEmployee).not.toHaveBeenCalled()
  })

  it('omits the creation line when the creation date cannot be read', () => {
    render(
      <BaselineNotice
        payload={JSON.stringify({ recordCreatedOn: 'not-a-date' })}
      />,
    )

    expect(screen.queryByText(/Record created/)).not.toBeInTheDocument()
    expect(screen.queryByText(/Invalid Date/)).not.toBeInTheDocument()
  })

  it('omits the creation line when the creation date was not recorded', () => {
    render(
      <BaselineNotice payload={JSON.stringify({ recordCreatedOn: null })} />,
    )

    expect(screen.getByText('Tracking started')).toBeInTheDocument()
    expect(screen.queryByText(/Record created/)).not.toBeInTheDocument()
  })
})
