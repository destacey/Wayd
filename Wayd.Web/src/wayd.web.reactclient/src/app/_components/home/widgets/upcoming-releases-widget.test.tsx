import type { Mock } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import UpcomingReleasesWidget, {
  groupByTargetDate,
  UPCOMING_RELEASES_DAYS,
  UPCOMING_RELEASES_LIMIT,
  upcomingDeliveries,
  VersionsPastTargetTile,
} from './upcoming-releases-widget'
import {
  TeamProductReleaseDto,
  TeamProductReleasesDto,
  TeamProductVersionDto,
} from '@/src/services/api/wayd.schemas'
import { asCalendarDate } from '@/src/utils/calendar-date'
import { tileValueStyle } from '../attention-tile-test-utils'
import { renderReportingWidget } from '../widget-display-test-utils'

const push = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push }),
}))

vi.mock('@/src/store/features/user-management/me-api', () => ({
  useGetMyUpcomingReleasesQuery: vi.fn(),
}))

import { useGetMyUpcomingReleasesQuery } from '@/src/store/features/user-management/me-api'

const mockSchedule = useGetMyUpcomingReleasesQuery as Mock

const planned = {
  id: 's-planned',
  name: 'Planned',
  category: 'Proposed',
  categoryLabel: 'Proposed',
  alias: 0,
} as const

const checkout = { id: 'p-1', key: 1, name: 'Checkout' }

const release = (
  key: number,
  targetDate: string,
  overrides: Partial<TeamProductReleaseDto> = {},
): TeamProductReleaseDto => ({
  id: `r-${key}`,
  key,
  version: `2026.${key}`,
  targetDate: asCalendarDate(targetDate),
  isPastTarget: false,
  status: planned,
  product: { id: 'p-9', key: 9, name: 'Shop' },
  teamProducts: [checkout],
  ...overrides,
})

const version = (
  key: number,
  targetDate: string,
  overrides: Partial<TeamProductVersionDto> = {},
): TeamProductVersionDto => ({
  id: `v-${key}`,
  key,
  number: `4.${key}.0`,
  targetDate: asCalendarDate(targetDate),
  isPastTarget: false,
  status: planned,
  product: checkout,
  ...overrides,
})

const loaded = (data: TeamProductReleasesDto) => ({
  data,
  isLoading: false,
  isError: false,
})

beforeEach(() => {
  vi.clearAllMocks()
})

describe('upcomingDeliveries', () => {
  it('puts past target first, then by date, a release before a version on the same day', () => {
    // Arrange
    const schedule: TeamProductReleasesDto = {
      releases: [release(2, '2026-04-06'), release(3, '2026-04-03')],
      versions: [
        version(1, '2026-04-06'),
        version(5, '2026-03-20', { isPastTarget: true }),
      ],
    }

    // Act
    const rows = upcomingDeliveries(schedule)

    // Assert
    expect(rows.map((r) => `${r.kind}-${r.key}`)).toEqual([
      'version-5',
      'release-3',
      'release-2',
      'version-1',
    ])
  })

  it('reads a list the caller may not view as empty', () => {
    // Arrange
    const schedule: TeamProductReleasesDto = {
      releases: null,
      versions: [version(1, '2026-04-06')],
    }

    // Act
    const rows = upcomingDeliveries(schedule)

    // Assert
    expect(rows.map((r) => r.kind)).toEqual(['version'])
  })

  it('names a release spanning product lines by the team products it reaches', () => {
    // Arrange
    const schedule: TeamProductReleasesDto = {
      releases: [
        release(1, '2026-04-06', {
          product: null,
          teamProducts: [{ id: 'p-2', key: 2, name: 'Cart' }, checkout],
        }),
      ],
      versions: [],
    }

    // Act
    const [row] = upcomingDeliveries(schedule)

    // Assert
    expect(row.products.map((p) => p.name)).toEqual(['Cart', 'Checkout'])
  })
})

describe('groupByTargetDate', () => {
  it('gathers everything past target under one heading, then one per date', () => {
    // Arrange
    const rows = upcomingDeliveries({
      releases: [release(1, '2026-03-01', { isPastTarget: true })],
      versions: [
        version(2, '2026-03-25', { isPastTarget: true }),
        version(3, '2026-04-06'),
        version(4, '2026-04-06'),
        version(5, '2026-04-08'),
      ],
    })

    // Act
    const groups = groupByTargetDate(rows)

    // Assert
    expect(groups.map((g) => [g.heading, g.rows.length])).toEqual([
      ['Past target', 2],
      ['Mon, Apr 6', 2],
      ['Wed, Apr 8', 1],
    ])
  })
})

describe('UpcomingReleasesWidget', () => {
  it('links each release and version under its date, past target first', () => {
    // Arrange
    mockSchedule.mockReturnValue(
      loaded({
        releases: [release(7, '2026-04-06', { name: 'Spring' })],
        versions: [version(3, '2026-03-30', { isPastTarget: true })],
      }),
    )

    // Act
    render(<UpcomingReleasesWidget title="Upcoming releases" />)

    // Assert
    const past = screen.getByText('Past target')
    const later = screen.getByText('Mon, Apr 6')
    expect(
      past.compareDocumentPosition(later) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
    expect(
      screen.getByRole('link', { name: 'Checkout 4.3.0' }),
    ).toHaveAttribute('href', '/product-management/versions/3')
    expect(
      screen.getByRole('link', { name: '2026.7 · Spring' }),
    ).toHaveAttribute('href', '/product-management/releases/7')
    const [versionRow, releaseRow] = screen.getAllByRole('listitem')
    expect(within(versionRow).getByText('Version')).toBeInTheDocument()
    expect(
      within(versionRow).getByText('Target Mar 30, 2026'),
    ).toBeInTheDocument()
    expect(within(releaseRow).getByText('Release')).toBeInTheDocument()
    expect(within(releaseRow).queryByText(/^Target /)).not.toBeInTheDocument()
    expect(within(releaseRow).getByText('Shop')).toBeInTheDocument()
    expect(within(releaseRow).getByText('Planned')).toBeInTheDocument()
  })

  it('asks for its window, 14 days until another is chosen', () => {
    // Arrange
    mockSchedule.mockReturnValue(loaded({ releases: [], versions: [] }))

    // Act
    render(<UpcomingReleasesWidget title="Upcoming releases" />)
    render(
      <UpcomingReleasesWidget
        title="Upcoming releases"
        settings={{ 'upcoming-window': '30' }}
      />,
    )

    // Assert
    expect(mockSchedule).toHaveBeenNthCalledWith(1, UPCOMING_RELEASES_DAYS)
    expect(mockSchedule).toHaveBeenLastCalledWith(30)
  })

  it('lists up to the limit, saying how many more', () => {
    // Arrange
    const many = Array.from({ length: UPCOMING_RELEASES_LIMIT + 2 }, (_, i) =>
      version(i + 1, '2026-04-06'),
    )
    mockSchedule.mockReturnValue(loaded({ releases: [], versions: many }))

    // Act
    render(<UpcomingReleasesWidget title="Upcoming releases" />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(
      UPCOMING_RELEASES_LIMIT,
    )
    expect(screen.getByText('And 2 more.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/product-management/versions',
    )
  })

  it('lists only as many as its Rows to show setting asks for', () => {
    // Arrange
    mockSchedule.mockReturnValue(
      loaded({
        releases: [release(1, '2026-04-02')],
        versions: [
          version(2, '2026-04-03'),
          version(3, '2026-04-04'),
          version(4, '2026-04-05'),
        ],
      }),
    )

    // Act
    render(
      <UpcomingReleasesWidget
        title="Upcoming releases"
        settings={{ rows: '3' }}
      />,
    )

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
    expect(screen.getByText('And 1 more.')).toBeInTheDocument()
  })

  it('shows a placeholder, and reports loading, while the schedule loads', () => {
    // Arrange
    mockSchedule.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    })

    // Act
    const { container, reportedState } = renderReportingWidget(
      <UpcomingReleasesWidget title="Upcoming releases" />,
    )

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    expect(reportedState()).toBe('loading')
  })

  it('shows a placeholder, not the last window, while a changed window loads', () => {
    // Arrange
    mockSchedule.mockReturnValue({
      data: { releases: [], versions: [version(1, '2026-05-20')] },
      currentData: undefined,
      isLoading: false,
      isFetching: true,
      isError: false,
    })

    // Act
    const { container, reportedState } = renderReportingWidget(
      <UpcomingReleasesWidget title="Upcoming releases" />,
    )

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Checkout 4.1.0' }),
    ).not.toBeInTheDocument()
    expect(reportedState()).toBe('loading')
  })

  it('reports rows when something is due', () => {
    // Arrange
    mockSchedule.mockReturnValue(
      loaded({ releases: [], versions: [version(1, '2026-04-06')] }),
    )

    // Act
    const { reportedState } = renderReportingWidget(
      <UpcomingReleasesWidget title="Upcoming releases" />,
    )

    // Assert
    expect(reportedState()).toBe('rows')
  })

  it('says how it fills, and reports empty, when nothing is due', () => {
    // Arrange
    mockSchedule.mockReturnValue(loaded({ releases: [], versions: [] }))

    // Act
    const { reportedState } = renderReportingWidget(
      <UpcomingReleasesWidget
        title="Upcoming releases"
        settings={{ 'upcoming-window': '30' }}
      />,
    )

    // Assert
    expect(
      screen.getByText(/Nothing is targeted to ship in the next 30 days/),
    ).toBeInTheDocument()
    expect(reportedState()).toBe('empty')
  })

  it('says, and reports failed, when the schedule could not load', () => {
    // Arrange
    mockSchedule.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    const { reportedState } = renderReportingWidget(
      <UpcomingReleasesWidget title="Upcoming releases" />,
    )

    // Assert
    expect(
      screen.getByText("Could not load your products' releases."),
    ).toBeInTheDocument()
    expect(reportedState()).toBe('failed')
  })
})

describe('VersionsPastTargetTile', () => {
  it('counts only the versions past target, in the warning colour', () => {
    // Arrange
    mockSchedule.mockReturnValue(
      loaded({
        releases: [release(1, '2026-03-01', { isPastTarget: true })],
        versions: [
          version(2, '2026-03-25', { isPastTarget: true }),
          version(3, '2026-03-26', { isPastTarget: true }),
          version(4, '2026-04-06'),
        ],
      }),
    )

    // Act
    const { container } = render(
      <VersionsPastTargetTile title="Versions past target" />,
    )

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Versions past target on your products: 2',
      }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-warning)')
    expect(mockSchedule).toHaveBeenCalledWith(UPCOMING_RELEASES_DAYS)
  })

  it('opens the versions grid', () => {
    // Arrange
    mockSchedule.mockReturnValue(
      loaded({
        releases: [],
        versions: [version(2, '2026-03-25', { isPastTarget: true })],
      }),
    )
    render(<VersionsPastTargetTile title="Versions past target" />)

    // Act
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Versions past target on your products: 1',
      }),
    )

    // Assert
    expect(push).toHaveBeenCalledWith('/product-management/versions')
  })

  it('reads zero, uncoloured, when nothing is past target', () => {
    // Arrange
    mockSchedule.mockReturnValue(
      loaded({ releases: [], versions: [version(4, '2026-04-06')] }),
    )

    // Act
    const { container } = render(
      <VersionsPastTargetTile title="Versions past target" />,
    )

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Versions past target on your products: 0',
      }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).not.toContain('var(--ant-color-warning)')
  })

  it('shows a dash rather than zero when the count failed', () => {
    // Arrange
    mockSchedule.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<VersionsPastTargetTile title="Versions past target" />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Versions past target on your products: unavailable',
      }),
    ).toBeInTheDocument()
  })
})
