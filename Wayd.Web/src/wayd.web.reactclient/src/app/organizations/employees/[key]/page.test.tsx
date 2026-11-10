import React, { Suspense } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EmployeeDetailsPage from './page'

const employee = {
  id: 'employee-1',
  key: 42,
  displayName: 'Ada Lovelace',
  isActive: true,
  email: 'ada.lovelace@acme.example',
  employeeNumber: 'E-0042',
  jobTitle: 'Principal Engineer',
}

vi.mock('next/dynamic', () => {
  return {
    default: () => {
      const DynamicComponent = ({ employeeId }: { employeeId: string }) => (
        <div data-testid="employee-cycle-time-report">{employeeId}</div>
      )
      return DynamicComponent
    },
  }
})

// The page renders RecordLayout, which reads the section from the URL and
// navigates with router.replace — so the route is the thing to assert on.
let mockSearchParams = new URLSearchParams()
const mockReplace = vi.fn((url: string) => {
  const query = url.split('?')[1] ?? ''
  mockSearchParams = new URLSearchParams(query)
})

// jsdom has no matchMedia, so useBreakpoint reports no md and RecordLayout
// renders its compact Select. Pin a wide viewport so both the section rail and
// the record facts rail are asserted — facts auto-collapse below xl.
vi.mock('antd', async () => {
  const actual = await vi.importActual<any>('antd')
  return {
    ...actual,
    Grid: {
      ...actual.Grid,
      useBreakpoint: () => ({ md: true, lg: true, xl: true }),
    },
  }
})

vi.mock('next/navigation', () => ({
  notFound: vi.fn(),
  usePathname: () => '/organizations/employees/42',
  useRouter: () => ({ replace: mockReplace, push: vi.fn() }),
  useSearchParams: () => mockSearchParams,
}))

vi.mock('@/src/components/common/page-title', () => {
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
  return { default: PageTitle }
})

vi.mock('@/src/components/common', () => ({
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

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    hasClaim: vi.fn(() => true),
    hasPermissionClaim: vi.fn(() => false),
  }),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    error: vi.fn(),
  }),
}))

vi.mock('@/src/hooks/use-document-title', () => ({
  useDocumentTitle: vi.fn(),
}))

vi.mock('@/src/hooks', async () => {
  const { useState } = await vi.importActual<typeof import('react')>('react')
  return {
    useAppDispatch: () => vi.fn(),
    useFeatureFlag: () => ({ isEnabled: false, isLoading: false }),
    // The facts rail remembers whether it is hidden. This mock replaces the
    // whole hooks barrel, so a plain state hook stands in for the real one.
    useLocalStorageState: (_key: string, initial: unknown) => useState(initial),
  }
})

vi.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeQuery: vi.fn(() => ({
    data: employee,
    isLoading: false,
    error: undefined,
  })),
  useGetDirectReportsQuery: vi.fn(() => ({
    data: [],
    isLoading: false,
  })),
}))

vi.mock('./_components/employee-capacity', () => {
  const MockEmployeeCapacity = () => <div data-testid="employee-capacity" />
  MockEmployeeCapacity.displayName = 'MockEmployeeCapacity'
  return { default: MockEmployeeCapacity }
})

vi.mock('./_components/employee-teams-grid', () => {
  const EmployeeTeamsGrid = () => <div>Employee Teams Grid</div>
  return { default: EmployeeTeamsGrid }
})

vi.mock(
  '@/src/app/organizations/employees/_components/delete-employee-form',
  () => {
    const DeleteEmployeeForm = () => <div>Delete Employee Form</div>
    return { default: DeleteEmployeeForm }
  },
)

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
    const params = Promise.resolve({ key: '42' }) as Promise<{
      key: string
    }> & {
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

  it('carries the employee facts in the record panel, not a Details section', async () => {
    // Arrange — the facts belong to the record, not to a section, so start on
    // the default one rather than inheriting the previous test's deep link.
    mockSearchParams = new URLSearchParams()
    const user = userEvent.setup()
    const params = Promise.resolve({ key: '42' }) as Promise<{
      key: string
    }> & {
      status: string
      value: { key: string }
    }
    params.status = 'fulfilled'
    params.value = { key: '42' }

    render(
      <Suspense fallback={<div>Loading employee page</div>}>
        <EmployeeDetailsPage params={params} />
      </Suspense>,
    )

    // Act — the panel is closed by default, so the content keeps the width.
    await user.click(
      await screen.findByRole('button', { name: 'Show Details' }),
    )

    // Assert — the facts are in the panel...
    expect(screen.getByText(employee.email)).toBeInTheDocument()
    expect(screen.getByText(employee.employeeNumber)).toBeInTheDocument()
    // ...and no Details section duplicates them in the rail beside it.
    expect(screen.queryByRole('tab', { name: /Details/ })).toBeNull()
  })
})
