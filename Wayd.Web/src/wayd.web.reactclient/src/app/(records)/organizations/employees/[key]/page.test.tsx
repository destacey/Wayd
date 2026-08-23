import React, { Suspense } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EmployeeDetailsPage from './page'

const employee = {
  id: 'employee-1',
  key: 42,
  displayName: 'Ada Lovelace',
  isActive: true,
}

jest.mock('next/dynamic', () => {
  return () => {
    const DynamicComponent = ({ employeeId }: { employeeId: string }) => (
      <div data-testid="employee-cycle-time-report">{employeeId}</div>
    )
    return DynamicComponent
  }
})

// The page renders RecordLayout, which reads the section from the URL and
// navigates with router.replace — so the route is the thing to assert on.
let mockSearchParams = new URLSearchParams()
const mockReplace = jest.fn((url: string) => {
  const query = url.split('?')[1] ?? ''
  mockSearchParams = new URLSearchParams(query)
})

// jsdom has no matchMedia, so useBreakpoint reports no md and RecordLayout
// renders its compact Select. Pin a desktop viewport so the rail is asserted.
jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  return {
    ...actual,
    Grid: { ...actual.Grid, useBreakpoint: () => ({ md: true, lg: true }) },
  }
})

jest.mock('next/navigation', () => ({
  notFound: jest.fn(),
  usePathname: () => '/organizations/employees/42',
  useRouter: () => ({ replace: mockReplace, push: jest.fn() }),
  useSearchParams: () => mockSearchParams,
}))

jest.mock('@/src/components/common/page-title', () => {
  const PageTitle = ({
    title,
    subtitle,
    actions,
  }: {
    title: React.ReactNode
    subtitle?: React.ReactNode
    actions?: React.ReactNode
  }) => (
    <div>
      <h1>{title}</h1>
      {subtitle && <div>{subtitle}</div>}
      {actions}
    </div>
  )
  return PageTitle
})

jest.mock('@/src/components/common', () => ({
  InactiveTag: ({ isActive }: { isActive: boolean }) => (
    <span>{isActive ? 'Active' : 'Inactive'}</span>
  ),
  PageActions: ({ actionItems }: { actionItems: any[] }) => {
    const buttons = actionItems.flatMap((item) => item.children ?? item)

    return (
      <div>
        {buttons.map((item) => (
          <button key={item.key} type="button" onClick={item.onClick}>
            {item.label}
          </button>
        ))}
      </div>
    )
  },
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasClaim: jest.fn(() => true),
    hasPermissionClaim: jest.fn(() => false),
  }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    error: jest.fn(),
  }),
}))

jest.mock('@/src/hooks/use-document-title', () => ({
  useDocumentTitle: jest.fn(),
}))

jest.mock('@/src/hooks', () => ({
  useAppDispatch: () => jest.fn(),
}))

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeQuery: jest.fn(() => ({
    data: employee,
    isLoading: false,
    error: undefined,
  })),
}))

jest.mock('./employee-details', () => {
  const EmployeeDetails = () => <div>Employee Details Content</div>
  return EmployeeDetails
})

jest.mock('./_components/employee-teams-grid', () => {
  const EmployeeTeamsGrid = () => <div>Employee Teams Grid</div>
  return EmployeeTeamsGrid
})

jest.mock('@/src/app/(legacy)/organizations/employees/_components/delete-employee-form', () => {
  const DeleteEmployeeForm = () => <div>Delete Employee Form</div>
  return DeleteEmployeeForm
})

describe('EmployeeDetailsPage', () => {
  it('reaches the cycle time report from the section rail', async () => {
    // Arrange
    const user = userEvent.setup()
    const params = Promise.resolve({ key: '42' }) as Promise<{
      key: string
    }> & { status: string; value: { key: string } }
    params.status = 'fulfilled'
    params.value = { key: '42' }

    render(
      <Suspense fallback={<div>Loading employee page</div>}>
        <EmployeeDetailsPage params={params} />
      </Suspense>,
    )

    expect(
      screen.queryByTestId('employee-cycle-time-report'),
    ).not.toBeInTheDocument()

    // Act — the rail is the only path now; the Actions menu is for actions,
    // not a second route to a section that is permanently listed.
    await user.click(await screen.findByRole('tab', { name: /Cycle Time/ }))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/organizations/employees/42?section=cycle-time-report',
      { scroll: false },
    )
  })

  it('renders the cycle time report when the URL selects it', async () => {
    // Arrange — arriving by deep link, the case tab state could not express
    mockSearchParams = new URLSearchParams('section=cycle-time-report')
    const params = Promise.resolve({ key: '42' }) as Promise<{ key: string }> & {
      status: string
      value: { key: string }
    }
    params.status = 'fulfilled'
    params.value = { key: '42' }

    // Act
    render(
      <Suspense fallback={<div>Loading employee page</div>}>
        <EmployeeDetailsPage params={params} />
      </Suspense>,
    )

    // Assert
    expect(
      await screen.findByTestId('employee-cycle-time-report'),
    ).toHaveTextContent(employee.id)
  })
})
