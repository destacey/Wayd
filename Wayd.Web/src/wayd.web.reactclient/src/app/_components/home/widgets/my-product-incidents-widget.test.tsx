import type { Mock } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import MyProductIncidentsWidget, {
  ActiveIncidentsTile,
  MY_PRODUCT_INCIDENTS_LIMIT,
} from './my-product-incidents-widget'
import { TeamProductIncidentDto } from '@/src/services/api/wayd.schemas'
import { toInstant } from '@/src/utils/instant'
import { tileValueStyle } from '../attention-tile-test-utils'
import { renderReportingWidget } from '../widget-display-test-utils'

const push = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push }),
}))

vi.mock('@/src/store/features/user-management/me-api', () => ({
  useGetMyIncidentsQuery: vi.fn(),
}))

import { useGetMyIncidentsQuery } from '@/src/store/features/user-management/me-api'

const mockIncidents = useGetMyIncidentsQuery as Mock

const HOUR = 60 * 60 * 1000

const incident = (
  key: number,
  title: string,
  overrides: Partial<TeamProductIncidentDto> = {},
): TeamProductIncidentDto => ({
  id: `i-${key}`,
  key,
  title,
  noticedAt: toInstant(new Date(Date.now() - 3 * HOUR)),
  severityLevel: {
    id: 'sev-1',
    name: 'SEV-1',
    sortOrder: 0,
    color: '#E07A4C',
  },
  status: {
    id: 's-open',
    name: 'Responding',
    category: 'Active',
    categoryLabel: 'Active',
    alias: 2,
  },
  products: [{ id: 'p-1', key: 1, name: 'Checkout' }],
  ...overrides,
})

const loaded = (data: TeamProductIncidentDto[]) => ({
  data,
  isLoading: false,
  isError: false,
})

beforeEach(() => {
  vi.clearAllMocks()
})

describe('MyProductIncidentsWidget', () => {
  it('links each incident with its severity, status, age and the caller’s products it is on', () => {
    // Arrange
    mockIncidents.mockReturnValue(
      loaded([
        incident(12, 'Checkout down', {
          products: [
            { id: 'p-2', key: 2, name: 'Billing' },
            { id: 'p-1', key: 1, name: 'Checkout' },
          ],
        }),
      ]),
    )

    // Act
    render(<MyProductIncidentsWidget title="Incidents on my products" />)

    // Assert
    expect(screen.getByRole('link', { name: 'Checkout down' })).toHaveAttribute(
      'href',
      '/product-management/incidents/12',
    )
    expect(screen.getByText('SEV-1')).toBeInTheDocument()
    expect(screen.getByText('Responding, active 3h')).toBeInTheDocument()
    expect(screen.getByText('Billing, Checkout')).toBeInTheDocument()
  })

  it('keeps the API’s order and lists up to the limit, saying how many more', () => {
    // Arrange
    const many = Array.from(
      { length: MY_PRODUCT_INCIDENTS_LIMIT + 2 },
      (_, i) => incident(i + 1, `Incident ${i + 1}`),
    )
    mockIncidents.mockReturnValue(loaded(many))

    // Act
    render(<MyProductIncidentsWidget title="Incidents on my products" />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(
      MY_PRODUCT_INCIDENTS_LIMIT,
    )
    expect(screen.getByRole('link', { name: 'Incident 1' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Incident 7' })).toBeNull()
    expect(screen.getByText('And 2 more.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/product-management/incidents',
    )
  })

  it('lists only as many as its Rows to show setting asks for', () => {
    // Arrange
    mockIncidents.mockReturnValue(
      loaded([1, 2, 3, 4].map((key) => incident(key, `Incident ${key}`))),
    )

    // Act
    render(
      <MyProductIncidentsWidget
        title="Incidents on my products"
        settings={{ rows: '3' }}
      />,
    )

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
    expect(screen.getByText('And 1 more.')).toBeInTheDocument()
  })

  it('shows a placeholder, and reports loading, while the incidents load', () => {
    // Arrange
    mockIncidents.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    })

    // Act
    const { container, reportedState } = renderReportingWidget(
      <MyProductIncidentsWidget title="Incidents on my products" />,
    )

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    expect(screen.queryByText(/No active incidents/)).not.toBeInTheDocument()
    expect(reportedState()).toBe('loading')
  })

  it('reports rows when an incident is active', () => {
    // Arrange
    mockIncidents.mockReturnValue(loaded([incident(1, 'Checkout down')]))

    // Act
    const { reportedState } = renderReportingWidget(
      <MyProductIncidentsWidget title="Incidents on my products" />,
    )

    // Assert
    expect(reportedState()).toBe('rows')
  })

  it('says how it fills, and reports empty, when no incident is active', () => {
    // Arrange
    mockIncidents.mockReturnValue(loaded([]))

    // Act
    const { reportedState } = renderReportingWidget(
      <MyProductIncidentsWidget title="Incidents on my products" />,
    )

    // Assert
    expect(
      screen.getByText(/No active incidents on your teams' products/),
    ).toBeInTheDocument()
    expect(reportedState()).toBe('empty')
  })

  it('says, and reports failed, when the incidents could not load', () => {
    // Arrange
    mockIncidents.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    const { reportedState } = renderReportingWidget(
      <MyProductIncidentsWidget title="Incidents on my products" />,
    )

    // Assert
    expect(
      screen.getByText("Could not load your products' incidents."),
    ).toBeInTheDocument()
    expect(reportedState()).toBe('failed')
  })
})

describe('ActiveIncidentsTile', () => {
  it('counts the active incidents in the error colour', () => {
    // Arrange
    mockIncidents.mockReturnValue(
      loaded([incident(1, 'Checkout down'), incident(2, 'Search slow')]),
    )

    // Act
    const { container } = render(
      <ActiveIncidentsTile title="Active incidents" />,
    )

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Active incidents on your products: 2',
      }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-error)')
  })

  it('opens the incidents grid', () => {
    // Arrange
    mockIncidents.mockReturnValue(loaded([incident(1, 'Checkout down')]))
    render(<ActiveIncidentsTile title="Active incidents" />)

    // Act
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Active incidents on your products: 1',
      }),
    )

    // Assert
    expect(push).toHaveBeenCalledWith('/product-management/incidents')
  })

  it('reads zero, uncoloured, when no incident is active', () => {
    // Arrange
    mockIncidents.mockReturnValue(loaded([]))

    // Act
    const { container } = render(
      <ActiveIncidentsTile title="Active incidents" />,
    )

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Active incidents on your products: 0',
      }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).not.toContain('var(--ant-color-error)')
  })

  it('names only its title while the count loads', () => {
    // Arrange
    mockIncidents.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    })

    // Act
    render(<ActiveIncidentsTile title="Active incidents" />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Active incidents' }),
    ).toBeInTheDocument()
  })

  it('shows a dash rather than zero when the count failed', () => {
    // Arrange
    mockIncidents.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<ActiveIncidentsTile title="Active incidents" />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Active incidents on your products: unavailable',
      }),
    ).toBeInTheDocument()
  })
})
