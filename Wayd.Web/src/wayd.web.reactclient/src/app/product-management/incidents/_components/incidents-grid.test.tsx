import { render, screen, waitFor, within } from '@testing-library/react'
import type { Mock } from 'vitest'
import userEvent from '@testing-library/user-event'
import type { FilterOptionsSource } from '@/src/components/common/wayd-grid'
import {
  IncidentSummaryDto,
  ProductStatusAlias,
} from '@/src/services/api/wayd.schemas'
import type { IncidentFilterField } from '@/src/store/features/product-management/incidents-api'
import IncidentsGrid from './incidents-grid'

const { incidentsGetIncidents, loads } = vi.hoisted(() => ({
  incidentsGetIncidents: vi.fn(),
  loads: new Map<string, Mock<FilterOptionsSource['load']>>(),
}))

vi.mock(import('@/src/services/api/incidents'), async (importOriginal) => ({
  ...(await importOriginal()),
  incidentsGetIncidents,
}))

const OPTIONS: Record<string, { value: string; label: string }[]> = {
  products: [
    { value: 'p1', label: 'API Gateway' },
    { value: 'p2', label: 'Pricing Portal' },
  ],
  commander: [{ value: 'c1', label: 'Addison Waldow' }],
}

vi.mock(
  import('@/src/store/features/product-management/incidents-api'),
  async (importOriginal) => ({
    ...(await importOriginal()),
    useIncidentFilterOptions:
      () =>
      (
        field: IncidentFilterField,
        extra: Omit<FilterOptionsSource, 'load'> = {},
      ): FilterOptionsSource => {
        const load =
          loads.get(field) ??
          vi.fn<FilterOptionsSource['load']>(async () => ({
            options: OPTIONS[field] ?? [],
            hasMore: false,
          }))
        loads.set(field, load)
        return { load, ...extra }
      },
  }),
)

const incident = (
  key: number,
  title: string,
  products: { id: string; key: number; name: string }[],
  releases: { id: string; key: number; name: string }[] = [],
): IncidentSummaryDto =>
  ({
    id: `i${key}`,
    key,
    title,
    noticedAt: '2026-11-10T12:00:00Z',
    commander: { id: 'c1', key: 1, name: 'Addison Waldow' },
    submittedBy: { id: 's1', key: 2, name: 'Casey Submitter' },
    reportedBy: { id: 'r1', key: 3, name: 'Riley Reporter' },
    severityLevel: {
      id: 'sev',
      name: 'SEV-1',
      sortOrder: 0,
      color: '#E07A4C',
    },
    status: { id: 's1', name: 'Open', category: 1, alias: 30 },
    products,
    releases,
  }) as unknown as IncidentSummaryDto

const ROWS = [
  incident(
    2,
    'Checkout down',
    [{ id: 'p1', key: 20, name: 'API Gateway' }],
    [{ id: 'r1', key: 3, name: 'Fulfillment Suite Q1 2025' }],
  ),
  incident(1, 'Quiet', [{ id: 'p3', key: 22, name: 'Messaging Catalog' }]),
]

const filterToggleOf = (header: string) => {
  const th = screen.getByText(header).closest('th')!
  const index = Array.from(th.parentElement!.children).indexOf(th)
  const below = th.parentElement!.nextElementSibling!.children[index]
  return within(below as HTMLElement).getByRole('button', {
    name: /Filter column/,
  })
}

const lastRequest = () => incidentsGetIncidents.mock.calls.at(-1)?.[0]

describe('IncidentsGrid', () => {
  beforeEach(() => {
    loads.clear()
    incidentsGetIncidents.mockReset()
    incidentsGetIncidents.mockResolvedValue({ items: ROWS, totalCount: 2 })
  })

  it('reads the incidents the filter bar names and links each product and release', async () => {
    // Act
    render(
      <IncidentsGrid
        statuses={[ProductStatusAlias.Open]}
        severityLevelIds={['sev']}
      />,
    )

    // Assert
    expect(await screen.findByText('Checkout down')).toBeInTheDocument()
    expect(lastRequest()).toMatchObject({
      page: 1,
      status: ['Open'],
      severityLevelId: ['sev'],
    })
    expect(screen.getByRole('link', { name: 'API Gateway' })).toHaveAttribute(
      'href',
      '/product-management/products/20',
    )
    expect(
      screen.getByRole('link', { name: 'Fulfillment Suite Q1 2025' }),
    ).toHaveAttribute('href', '/product-management/releases/3')
    expect(screen.getByText('2 incidents')).toBeInTheDocument()
  })

  it('sends no state or severity when the filter bar names none', async () => {
    // Act
    render(<IncidentsGrid statuses={[]} severityLevelIds={[]} />)
    await screen.findByText('Checkout down')

    // Assert
    expect(lastRequest()).toMatchObject({
      status: undefined,
      severityLevelId: undefined,
    })
  })

  it('filters by a product its filter loads, matched by id', async () => {
    // Arrange
    render(<IncidentsGrid statuses={[]} severityLevelIds={[]} />)
    await screen.findByText('Checkout down')

    // Act
    await userEvent.click(filterToggleOf('Products'))
    const option = await screen.findByText('Pricing Portal')
    await userEvent.click(
      within(option.closest('label')!).getByRole('checkbox'),
    )

    // Assert
    expect(loads.get('products')).toHaveBeenCalled()
    expect(loads.get('reportedBy')).not.toHaveBeenCalled()
    await waitFor(() =>
      expect(lastRequest()).toMatchObject({ filter: 'products=p2' }),
    )
  })

  it('filters to incidents with no commander', async () => {
    // Arrange
    render(<IncidentsGrid statuses={[]} severityLevelIds={[]} />)
    await screen.findByText('Checkout down')

    // Act
    await userEvent.click(filterToggleOf('Commander'))
    const blanks = await screen.findByText('(Blanks)')
    await userEvent.click(
      within(blanks.closest('label')!).getByRole('checkbox'),
    )

    // Assert
    await waitFor(() =>
      expect(lastRequest()).toMatchObject({ filter: 'commander=' }),
    )
  })

  it('sorts by a person on the server, and not by products', async () => {
    // Arrange
    render(<IncidentsGrid statuses={[]} severityLevelIds={[]} />)
    await screen.findByText('Checkout down')

    // Act
    await userEvent.click(screen.getByText('Products'))
    await userEvent.click(screen.getByText('Reported by'))

    // Assert
    await waitFor(() =>
      expect(lastRequest()).toMatchObject({ sort: 'reportedBy asc' }),
    )
    expect(
      incidentsGetIncidents.mock.calls.some(([request]) =>
        request.sort?.includes('products'),
      ),
    ).toBe(false)
  })
})
