import {
  DeliveryRecordKind,
  ProductStatusAlias,
  RecentDeliveryEventDto,
} from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import dayjs from 'dayjs'
import RecentDeliveryActivity from './recent-delivery-activity'

// The global mock stubs dayjs to formatting only; this component compares dates.
jest.unmock('dayjs')

const event = (
  overrides: Partial<RecentDeliveryEventDto> = {},
): RecentDeliveryEventDto =>
  ({
    recordId: 'record-1',
    recordKey: 12,
    kind: DeliveryRecordKind.Version,
    product: { id: 'product-1', key: 3, name: 'checkout-api' },
    label: '4.8.2',
    statusName: 'Released',
    alias: ProductStatusAlias.Released,
    changedOn: '2026-04-20T09:42:00Z',
    ...overrides,
  }) as unknown as RecentDeliveryEventDto

describe('RecentDeliveryActivity', () => {
  it('names the product and the version that changed', () => {
    // Arrange / Act
    render(<RecentDeliveryActivity events={[event()]} />)

    // Assert
    expect(screen.getByText('checkout-api')).toBeInTheDocument()
    expect(screen.getByText('4.8.2')).toBeInTheDocument()
  })

  it('links a version to its record and a package to its own', () => {
    // Arrange — the two live on different pages, so the feed cannot link them the same way.
    const events = [
      event(),
      event({
        recordId: 'record-2',
        recordKey: 5,
        kind: DeliveryRecordKind.ReleasePackage,
        product: undefined,
        label: 'OFT-2026.07',
        componentCount: 15,
      }),
    ]

    // Act
    render(<RecentDeliveryActivity events={events} />)

    // Assert
    expect(screen.getByRole('link', { name: 'checkout-api' })).toHaveAttribute(
      'href',
      '/product-management/versions/12',
    )
    expect(screen.getByRole('link', { name: 'OFT-2026.07' })).toHaveAttribute(
      'href',
      '/product-management/release-packages/5',
    )
  })

  it('says how much a package carried, since it names no single product', () => {
    // Arrange / Act
    render(
      <RecentDeliveryActivity
        events={[
          event({
            kind: DeliveryRecordKind.ReleasePackage,
            product: undefined,
            label: 'OFT-2026.07',
            componentCount: 15,
          }),
        ]}
      />,
    )

    // Assert
    expect(screen.getByText(/Package · 15 components/)).toBeInTheDocument()
  })

  it('says what a withdrawal pulled, not just that it happened', () => {
    // Arrange — the event alone leaves a reader asking when the thing had gone out.
    // Act
    render(
      <RecentDeliveryActivity
        events={[
          event({
            statusName: 'Withdrawn',
            alias: ProductStatusAlias.Withdrawn,
            releasedDate: '2026-04-19' as unknown as Date,
          }),
        ]}
      />,
    )

    // Assert
    expect(screen.getByText(/Withdrawn/)).toBeInTheDocument()
    expect(screen.getByText(/released 19 Apr/)).toBeInTheDocument()
  })

  it('marks a cut version as still waiting to ship', () => {
    // Arrange — Ready with no released date is a version sitting in the queue, which is the state
    // most worth noticing in a feed of things that shipped.
    // Act
    render(
      <RecentDeliveryActivity
        events={[
          event({ statusName: 'Cut', alias: ProductStatusAlias.Ready }),
        ]}
      />,
    )

    // Assert
    expect(screen.getByText(/awaiting release/)).toBeInTheDocument()
  })

  it('keeps a renamed status readable while styling it by meaning', () => {
    // Arrange — an organization that calls it "Shipped" still gets the released treatment, because
    // the appearance is keyed on the alias rather than the name.
    // Act
    render(
      <RecentDeliveryActivity
        events={[
          event({ statusName: 'Shipped', alias: ProductStatusAlias.Released }),
        ]}
      />,
    )

    // Assert
    expect(screen.getByText(/Shipped/)).toBeInTheDocument()
  })

  it('shows the time alone for something that happened today', () => {
    // Arrange — repeating today's date on every row is noise in a feed of what just happened.
    const today = dayjs().hour(9).minute(42).second(0)

    // Act
    render(<RecentDeliveryActivity events={[event({ changedOn: today.toISOString() as unknown as Date })]} />)

    // Assert
    expect(screen.getByText(/· 09:42$/)).toBeInTheDocument()
  })

  it('keeps the year on an event from a previous one', () => {
    // Arrange — without it, a December event reads as this December once January arrives.
    const lastYear = dayjs().subtract(1, 'year').month(11).date(12).hour(14).minute(30)

    // Act
    render(
      <RecentDeliveryActivity
        events={[event({ changedOn: lastYear.toISOString() as unknown as Date })]}
      />,
    )

    // Assert
    expect(
      screen.getByText(new RegExp(`12 Dec ${lastYear.year()} 14:30`)),
    ).toBeInTheDocument()
  })

  it('says so when nothing has happened', () => {
    // Arrange / Act
    render(<RecentDeliveryActivity events={[]} />)

    // Assert
    expect(screen.getByText('Nothing has happened yet.')).toBeInTheDocument()
  })
})
