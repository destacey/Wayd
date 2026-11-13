import type { Mock } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import MyWorkItemsWidget, {
  groupMyWorkItems,
  MY_WORK_ITEMS_LIMIT,
  recentlyDoneSince,
} from './my-work-items-widget'
import { StatusCategoryId } from '@/src/components/types'
import { StatusCategory, WorkItemListDto } from '@/src/services/wayd-api'
import { renderReportingWidget } from '../widget-display-test-utils'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/user-management/me-api', () => ({
  useGetMyWorkItemsQuery: vi.fn(),
}))

import { useGetMyWorkItemsQuery } from '@/src/store/features/user-management/me-api'

const mockWorkItems = useGetMyWorkItemsQuery as Mock

const categoryNames: Record<StatusCategoryId, string> = {
  [StatusCategoryId.Proposed]: 'Proposed',
  [StatusCategoryId.Active]: 'Active',
  [StatusCategoryId.Done]: 'Done',
  [StatusCategoryId.Removed]: 'Removed',
}

const workItem = (
  key: string,
  category: StatusCategoryId,
  overrides: Partial<WorkItemListDto> = {},
): WorkItemListDto =>
  ({
    id: `id-${key}`,
    key,
    title: `Title ${key}`,
    workspace: { id: 'ws-1', key: 'CORE', name: 'Core' },
    type: { id: 1, name: 'Story', level: { id: 1, name: 'Story', order: 1 } },
    status: category === StatusCategoryId.Active ? 'In Progress' : 'New',
    statusCategory: { id: category, name: categoryNames[category] },
    stackRank: 1,
    created: new Date('2026-10-01T00:00:00Z'),
    tags: [],
    ...overrides,
  }) as WorkItemListDto

const loaded = (data: WorkItemListDto[] | undefined) => ({
  data,
  isLoading: false,
  isError: false,
})

/** Answers the open request with `open` and the done request with `done`. */
const respond = (
  open: ReturnType<typeof loaded>,
  done: ReturnType<typeof loaded> = loaded([]),
) =>
  mockWorkItems.mockImplementation(({ doneFrom }) => (doneFrom ? done : open))

beforeEach(() => {
  vi.clearAllMocks()
})

describe('groupMyWorkItems', () => {
  it('puts Active work items before Proposed, keeping the order within each', () => {
    // Arrange
    const items = [
      workItem('CORE-1', StatusCategoryId.Proposed),
      workItem('CORE-2', StatusCategoryId.Active),
      workItem('CORE-3', StatusCategoryId.Proposed),
      workItem('CORE-4', StatusCategoryId.Active),
    ]

    // Act
    const grouped = groupMyWorkItems(items)

    // Assert
    expect(grouped.map((w) => w.key)).toEqual([
      'CORE-2',
      'CORE-4',
      'CORE-1',
      'CORE-3',
    ])
  })
})

describe('recentlyDoneSince', () => {
  it('starts at the beginning of the day six days before today, seven days in all', () => {
    // Arrange
    const now = new Date(2026, 10, 11, 15, 30)

    // Act
    const since = new Date(recentlyDoneSince(now))

    // Assert
    expect(since).toEqual(new Date(2026, 10, 5, 0, 0, 0, 0))
  })
})

describe('MyWorkItemsWidget', () => {
  it('asks for open work and for work done in the last week', () => {
    // Arrange
    respond(loaded([]))

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    expect(mockWorkItems).toHaveBeenCalledWith({
      statusCategories: [StatusCategory.Active, StatusCategory.Proposed],
    })
    expect(mockWorkItems).toHaveBeenCalledWith({
      statusCategories: [StatusCategory.Done],
      doneFrom: recentlyDoneSince(),
    })
  })

  it('lists Active work items first, each linking to the work item', () => {
    // Arrange
    respond(
      loaded([
        workItem('CORE-1', StatusCategoryId.Proposed),
        workItem('CORE-2', StatusCategoryId.Active, {
          sprint: { id: 's-1', key: 7, name: 'Sprint 7' },
        } as Partial<WorkItemListDto>),
      ]),
    )

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    const items = screen.getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(
      within(items[0]).getByRole('link', { name: 'CORE-2 · Title CORE-2' }),
    ).toHaveAttribute('href', '/work/workspaces/CORE/work-items/CORE-2')
    expect(within(items[0]).getByText('In Progress')).toBeInTheDocument()
    expect(within(items[0]).getByText('Sprint 7')).toBeInTheDocument()
    expect(
      within(items[1]).getByRole('link', { name: 'CORE-1 · Title CORE-1' }),
    ).toHaveAttribute('href', '/work/workspaces/CORE/work-items/CORE-1')
  })

  it('links a synced work item to its external system', () => {
    // Arrange
    respond(
      loaded([
        workItem('CORE-1', StatusCategoryId.Active, {
          externalViewWorkItemUrl:
            'https://dev.azure.com/org/proj/_workitems/edit/1',
        }),
        workItem('CORE-2', StatusCategoryId.Active),
      ]),
    )

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    const items = screen.getAllByRole('listitem')
    const external = within(items[0]).getByTitle('Open in external system')
    expect(external).toHaveAttribute(
      'href',
      'https://dev.azure.com/org/proj/_workitems/edit/1',
    )
    expect(external).toHaveAttribute('target', '_blank')
    expect(
      within(items[1]).queryByTitle('Open in external system'),
    ).not.toBeInTheDocument()
  })

  it('shows the team of a work item outside a sprint', () => {
    // Arrange
    respond(
      loaded([
        workItem('CORE-3', StatusCategoryId.Active, {
          team: { id: 't-1', key: 4, name: 'Platform' },
        } as Partial<WorkItemListDto>),
      ]),
    )

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    expect(screen.getByText('Platform')).toBeInTheDocument()
  })

  it('lists the first work items and counts the rest', () => {
    // Arrange
    respond(
      loaded(
        Array.from({ length: MY_WORK_ITEMS_LIMIT + 2 }, (_, i) =>
          workItem(`CORE-${i}`, StatusCategoryId.Active),
        ),
      ),
    )

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(MY_WORK_ITEMS_LIMIT)
    expect(screen.getByText('And 2 more work items.')).toBeInTheDocument()
  })

  it('counts the work items done in the last seven days', () => {
    // Arrange
    respond(
      loaded([workItem('CORE-1', StatusCategoryId.Active)]),
      loaded([
        workItem('CORE-8', StatusCategoryId.Done),
        workItem('CORE-9', StatusCategoryId.Done),
      ]),
    )

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    expect(
      screen.getByText('2 work items done in the last 7 days.'),
    ).toBeInTheDocument()
  })

  it('says how it fills, and still counts what was done, when nothing is open', () => {
    // Arrange
    respond(loaded([]), loaded([workItem('CORE-8', StatusCategoryId.Done)]))

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    expect(
      screen.getByText(/Nothing is assigned to you in progress or proposed/),
    ).toBeInTheDocument()
    expect(
      screen.getByText('1 work item done in the last 7 days.'),
    ).toBeInTheDocument()
  })

  it('leaves the done count out when nothing was done', () => {
    // Arrange
    respond(loaded([workItem('CORE-1', StatusCategoryId.Active)]), loaded([]))

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    expect(screen.queryByText(/done in the last/)).not.toBeInTheDocument()
  })

  it('leaves the done count out when it could not be loaded', () => {
    // Arrange
    respond(loaded([workItem('CORE-1', StatusCategoryId.Active)]), {
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(1)
    expect(screen.queryByText(/done in the last/)).not.toBeInTheDocument()
  })

  it('says the work items failed to load rather than that there are none', () => {
    // Arrange
    respond({ data: undefined, isLoading: false, isError: true })

    // Act
    render(<MyWorkItemsWidget title="My work items" />)

    // Assert
    expect(
      screen.getByText('Could not load your work items.'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/Nothing is assigned/)).not.toBeInTheDocument()
  })

  describe('reported state', () => {
    it('reports loading while the open work items load', () => {
      // Arrange
      respond({ data: undefined, isLoading: true, isError: false })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyWorkItemsWidget title="My work items" />,
      )

      // Assert
      expect(reportedState()).toBe('loading')
    })

    it('reports failed, though there is no data, when the open query errors', () => {
      // Arrange
      respond({ data: undefined, isLoading: false, isError: true })

      // Act
      const { reportedState } = renderReportingWidget(
        <MyWorkItemsWidget title="My work items" />,
      )

      // Assert
      expect(reportedState()).toBe('failed')
    })

    it('reports empty when nothing is open, even though work was done', () => {
      // Arrange
      respond(loaded([]), loaded([workItem('CORE-8', StatusCategoryId.Done)]))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyWorkItemsWidget title="My work items" />,
      )

      // Assert
      expect(reportedState()).toBe('empty')
    })

    it('reports empty when the open query returns only other categories', () => {
      // Arrange
      respond(loaded([workItem('CORE-8', StatusCategoryId.Done)]))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyWorkItemsWidget title="My work items" />,
      )

      // Assert
      expect(reportedState()).toBe('empty')
    })

    it.each([
      ['Active', StatusCategoryId.Active],
      ['Proposed', StatusCategoryId.Proposed],
    ])('reports rows when a work item is %s', (_, category) => {
      // Arrange
      respond(loaded([workItem('CORE-1', category)]))

      // Act
      const { reportedState } = renderReportingWidget(
        <MyWorkItemsWidget title="My work items" />,
      )

      // Assert
      expect(reportedState()).toBe('rows')
    })
  })
})
