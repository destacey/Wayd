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

jest.mock('antd', () => ({
  Card: ({
    tabList,
    activeTabKey,
    onTabChange,
    children,
  }: {
    tabList: Array<{ key: string; tab: React.ReactNode }>
    activeTabKey: string
    onTabChange: (key: string) => void
    children: React.ReactNode
  }) => (
    <div>
      <div role="tablist">
        {tabList.map((tab) => (
          <button
            key={tab.key}
            aria-selected={activeTabKey === tab.key}
            role="tab"
            type="button"
            onClick={() => onTabChange(tab.key)}
          >
            {tab.tab}
          </button>
        ))}
      </div>
      <div>{children}</div>
    </div>
  ),
  Spin: () => <div>Loading</div>,
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

jest.mock('../_components/delete-employee-form', () => {
  const DeleteEmployeeForm = () => <div>Delete Employee Form</div>
  return DeleteEmployeeForm
})

describe('EmployeeDetailsPage', () => {
  it('opens the employee cycle time report from page actions', async () => {
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

    await user.click(
      await screen.findByRole('button', { name: 'Cycle Time Report' }),
    )

    expect(
      screen.getByRole('tab', { name: /Cycle Time Report/ }),
    ).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByTestId('employee-cycle-time-report')).toHaveTextContent(
      employee.id,
    )
  })
})
