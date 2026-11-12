import { ComponentType, Suspense, act } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { App, Modal } from 'antd'
import {
  IncidentWorkflowAction,
  ProductStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'
import IncidentDetailsPage from './page'

const incident = {
  id: '01a1d091-2de1-75d3-97d9-34eae573d1fc',
  key: 1,
  title: 'Checkout latency',
  commander: { id: 'emp-1', key: 4, name: 'Addison Waldow' },
  submittedBy: { id: 'emp-2', key: 5, name: 'Casey Submitter' },
  reportedBy: { id: 'emp-3', key: 6, name: 'Riley Reporter' },
  severityLevel: {
    id: 'sev-1',
    name: 'High',
    sortOrder: 1,
    color: '#E07A4C',
  },
  noticedAt: new Date('2026-11-12T16:00:00Z'),
  products: [],
  releases: [],
  status: {
    id: 'status-1',
    name: 'Open',
    category: StatusCategory.Active,
    categoryLabel: 'Active',
    alias: 0,
  },
  statusAlias: ProductStatusAlias.Open,
}

vi.mock(import('antd'), async (importOriginal) => {
  const actual = await importOriginal()
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
  usePathname: () => '/product-management/incidents/1',
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}))

vi.mock(import('@/src/components/common'), async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    PageActions: (({
      actionItems = [],
    }: {
      actionItems?: {
        type?: string
        key?: string
        label?: string
        onClick?: () => void
      }[]
    }) => (
      <div>
        {actionItems.map((item, index) =>
          item.type === 'divider' ? (
            <hr key={item.key ?? index} />
          ) : (
            <button key={item.key} type="button" onClick={item.onClick}>
              {item.label}
            </button>
          ),
        )}
      </div>
    )) as (typeof actual)['PageActions'],
  }
})

vi.mock('@/src/components/common/follow-button', () => ({
  default: () => null,
}))

vi.mock(
  '@/src/app/product-management/incidents/_components/incident-facts',
  () => ({
    default: () => null,
  }),
)

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    hasPermissionClaim: () => true,
    user: undefined,
  }),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock(import('@/src/hooks'), async (importOriginal) => ({
  ...(await importOriginal()),
  useDocumentTitle: vi.fn(),
}))

vi.mock('@/src/components/hoc', () => ({
  authorizePage: (Component: ComponentType) => Component,
  requireFeatureFlag: (Component: ComponentType) => Component,
}))

vi.mock('@/src/store/features/product-management/incidents-api', () => ({
  useGetIncidentQuery: () => ({
    data: incident,
    error: undefined,
    isLoading: false,
    refetch: vi.fn(),
  }),
  useUpdateIncidentDetailsMutation: () => [vi.fn(), { isLoading: false }],
  useApplyIncidentWorkflowActionMutation: () => [
    vi.fn(),
    { isLoading: false },
  ],
  useGetIncidentWorkflowActionsQuery: () => ({
    data: [{ action: IncidentWorkflowAction.BeginResponding }],
    isError: false,
    isLoading: false,
  }),
  useGetIncidentStatusWorkflowQuery: () => ({ data: undefined }),
}))

vi.mock('@/src/store/features/product-management/products-api', () => ({
  useGetProductsQuery: () => ({ data: [] }),
}))

vi.mock('@/src/store/features/product-management/releases-api', () => ({
  useGetReleasesQuery: () => ({ data: [] }),
}))

vi.mock(
  '@/src/store/features/product-management/incident-severity-sets-api',
  () => ({
    useGetIncidentSeveritySetsQuery: () => ({ data: [] }),
  }),
)

vi.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeesQuery: () => ({ data: [] }),
}))

const renderPage = () =>
  act(async () => {
    render(
      <App>
        <Suspense>
          <IncidentDetailsPage params={Promise.resolve({ key: '1' })} />
        </Suspense>
      </App>,
    )
  })

describe('IncidentDetailsPage status confirmation', () => {
  it('confirms a status transition with the themed app modal', async () => {
    // Arrange
    const staticConfirm = vi.spyOn(Modal, 'confirm')
    const user = userEvent.setup()
    await renderPage()

    // Act
    await user.click(screen.getByRole('button', { name: 'Begin responding' }))

    // Assert
    expect(
      await screen.findByText('Mark this incident as actively being worked.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Response started at')).toBeInTheDocument()
    expect(staticConfirm).not.toHaveBeenCalled()
    staticConfirm.mockRestore()
  })
})
