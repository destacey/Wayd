import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IncidentSummaryDto } from '@/src/services/wayd-api'
import IncidentsGrid from './incidents-grid'

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
    noticedAt: new Date('2026-11-10T12:00:00Z'),
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

const filterToggleOf = (header: string) => {
  const selector = '[class*="filterButton"], .anticon-filter'
  const th = screen.getByText(header).closest('th')!
  const index = Array.from(th.parentElement!.children).indexOf(th)
  const below = th.parentElement!.nextElementSibling?.children[index]
  const toggle =
    th.querySelector(selector) ?? below?.querySelector(selector) ?? null
  expect(toggle).toBeTruthy()
  return toggle as Element
}

describe('IncidentsGrid', () => {
  it('filters by each linked product and release', async () => {
    // Arrange
    render(
      <IncidentsGrid
        isLoading={false}
        incidents={[
          incident(
            1,
            'Checkout down',
            [
              { id: 'p1', key: 20, name: 'API Gateway' },
              { id: 'p2', key: 21, name: 'Pricing Portal' },
            ],
            [
              { id: 'r1', key: 3, name: 'Fulfillment Suite Q1 2025' },
              { id: 'r2', key: 1, name: 'Fulfillment Suite Q4 2024' },
            ],
          ),
          incident(2, 'Quiet', [{ id: 'p3', key: 22, name: 'Messaging Catalog' }]),
        ]}
      />,
    )

    // Act
    await userEvent.click(filterToggleOf('Products'))

    // Assert — the collapsed "Name +1" label cannot be filtered; each product is its own value
    expect(await screen.findByRole('checkbox', { name: 'API Gateway' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Pricing Portal' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Messaging Catalog' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'API Gateway' })).toHaveAttribute(
      'href',
      '/product-management/products/20',
    )

    await userEvent.click(filterToggleOf('Releases'))
    expect(
      await screen.findByRole('checkbox', { name: 'Fulfillment Suite Q1 2025' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('checkbox', { name: 'Fulfillment Suite Q4 2024' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Fulfillment Suite Q1 2025' }),
    ).toHaveAttribute('href', '/product-management/releases/3')
  })
})
