import type { Mock } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import MyDeliverablesWidget, {
  deliverablesDueSoon,
  MY_DELIVERABLES_LIMIT,
} from './my-deliverables-widget'
import {
  MyProjectDeliverableDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import { renderReportingWidget } from '../widget-display-test-utils'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/ppm/project-deliverables-api', () => ({
  useGetMyProjectDeliverablesQuery: vi.fn(),
}))

vi.mock(import('@/src/utils'), async (importOriginal) => ({
  ...(await importOriginal()),
  todayCalendarDate: () => '2026-11-11',
}))

import { useGetMyProjectDeliverablesQuery } from '@/src/store/features/ppm/project-deliverables-api'

const mockDeliverables = useGetMyProjectDeliverablesQuery as Mock

const deliverable = (
  id: number,
  name: string,
  targetDate?: string,
): MyProjectDeliverableDto => ({
  id: `d-${id}`,
  name,
  targetDate,
  status: {
    id: 'status-1',
    name: 'In Progress',
    category: StatusCategory.Active,
    categoryLabel: 'Active',
    alias: 2,
  },
  project: { id: 'p-1', key: 'APOLLO', name: 'Apollo' },
})

const loaded = (data: MyProjectDeliverableDto[]) => ({
  data,
  isLoading: false,
  isError: false,
})

beforeEach(() => {
  vi.clearAllMocks()
})

describe('deliverablesDueSoon', () => {
  it('keeps past-target deliverables and those due within 14 days, counting the rest', () => {
    // Arrange
    const deliverables = [
      deliverable(1, 'Late', '2026-10-01'),
      deliverable(2, 'Day fourteen', '2026-11-25'),
      deliverable(3, 'Day fifteen', '2026-11-26'),
      deliverable(4, 'Undated'),
    ]

    // Act
    const { soon, laterCount } = deliverablesDueSoon(deliverables, '2026-11-11')

    // Assert
    expect(soon.map((d) => d.name)).toEqual(['Late', 'Day fourteen'])
    expect(laterCount).toBe(2)
  })
})

describe('MyDeliverablesWidget with a longer window', () => {
  it('lists deliverables due within it, and says so when none is', () => {
    // Arrange
    mockDeliverables.mockReturnValue(
      loaded([
        deliverable(1, 'Day thirty', '2026-12-11'),
        deliverable(2, 'Day thirty-one', '2026-12-12'),
      ]),
    )

    // Act
    render(
      <MyDeliverablesWidget
        title="Deliverables I own"
        settings={{ 'due-window': '30' }}
      />,
    )

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(1)
    expect(screen.getByText('Day thirty')).toBeInTheDocument()
  })

  it('names the window when nothing falls within it', () => {
    // Arrange
    mockDeliverables.mockReturnValue(
      loaded([deliverable(1, 'Far off', '2027-03-01')]),
    )

    // Act
    render(
      <MyDeliverablesWidget
        title="Deliverables I own"
        settings={{ 'due-window': '60' }}
      />,
    )

    // Assert
    expect(
      screen.getByText(/None is due in the next 60 days\./),
    ).toBeInTheDocument()
  })
})

describe('MyDeliverablesWidget', () => {
  it('lists each deliverable with its status, target and project, marking one past target', () => {
    // Arrange
    mockDeliverables.mockReturnValue(
      loaded([
        deliverable(1, 'Portal', '2026-11-04'),
        deliverable(2, 'Reports', '2026-11-11'),
        deliverable(3, 'Migration', '2026-12-31'),
        deliverable(4, 'Training'),
      ]),
    )

    // Act
    render(<MyDeliverablesWidget title="Deliverables I own" />)

    // Assert
    const items = screen.getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(
      within(items[0]).getByRole('link', { name: 'Portal' }),
    ).toHaveAttribute('href', '/ppm/projects/APOLLO?section=deliverables')
    expect(within(items[0]).getByText(/\(past target\)/)).toBeInTheDocument()
    expect(within(items[0]).getByText('In Progress')).toBeInTheDocument()
    expect(
      within(items[0]).getByRole('link', { name: 'Apollo' }),
    ).toHaveAttribute('href', '/ppm/projects/APOLLO')
    expect(within(items[1]).queryByText(/past target/)).not.toBeInTheDocument()
    expect(
      screen.getByText('You own 2 more open deliverables due later.'),
    ).toBeInTheDocument()
  })

  it('says none is due soon, and how many are due later, when every one is later', () => {
    // Arrange
    mockDeliverables.mockReturnValue(loaded([deliverable(1, 'Training')]))

    // Act
    render(<MyDeliverablesWidget title="Deliverables I own" />)

    // Assert
    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
    expect(
      screen.getByText(
        'None is due in the next 14 days. You own 1 open deliverable due later.',
      ),
    ).toBeInTheDocument()
  })

  it('lists the first deliverables and counts the rest', () => {
    // Arrange
    mockDeliverables.mockReturnValue(
      loaded(
        Array.from({ length: MY_DELIVERABLES_LIMIT + 2 }, (_, i) =>
          deliverable(i, `Deliverable ${i}`, '2026-11-12'),
        ),
      ),
    )

    // Act
    render(<MyDeliverablesWidget title="Deliverables I own" />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(MY_DELIVERABLES_LIMIT)
    expect(
      screen.getByText('And 2 more due in the next 14 days.'),
    ).toBeInTheDocument()
  })

  it('says how it fills when the caller owns none', () => {
    // Arrange
    mockDeliverables.mockReturnValue(loaded([]))

    // Act
    render(<MyDeliverablesWidget title="Deliverables I own" />)

    // Assert
    expect(screen.getByText(/You own no open deliverables/)).toBeInTheDocument()
  })

  it('says the deliverables failed to load rather than that there are none', () => {
    // Arrange
    mockDeliverables.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<MyDeliverablesWidget title="Deliverables I own" />)

    // Assert
    expect(
      screen.getByText('Could not load your deliverables.'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/You own no/)).not.toBeInTheDocument()
  })

  describe('reported state', () => {
    it('reports loading while the deliverables load', () => {
      // Arrange
      mockDeliverables.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
      })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyDeliverablesWidget title="Deliverables I own" />,
      )

      // Assert
      expect(reportedState()).toBe('loading')
    })

    it('reports failed, though there is no data, when the query errors', () => {
      // Arrange
      mockDeliverables.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
      })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyDeliverablesWidget title="Deliverables I own" />,
      )

      // Assert
      expect(reportedState()).toBe('failed')
    })

    it('reports empty when the caller owns no deliverables', () => {
      // Arrange
      mockDeliverables.mockReturnValue(loaded([]))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyDeliverablesWidget title="Deliverables I own" />,
      )

      // Assert
      expect(reportedState()).toBe('empty')
    })

    it('reports rows when a deliverable is due soon', () => {
      // Arrange
      mockDeliverables.mockReturnValue(
        loaded([deliverable(1, 'Portal', '2026-11-12')]),
      )

      // Act
      const { reportedState } = renderReportingWidget(
        <MyDeliverablesWidget title="Deliverables I own" />,
      )

      // Assert
      expect(reportedState()).toBe('rows')
    })

    it('reports rows, not empty, when every deliverable is due later', () => {
      // Arrange
      mockDeliverables.mockReturnValue(loaded([deliverable(1, 'Training')]))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyDeliverablesWidget title="Deliverables I own" />,
      )

      // Assert
      expect(reportedState()).toBe('rows')
    })
  })
})
