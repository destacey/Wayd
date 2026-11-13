import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import FollowingWidget, { FOLLOWING_COUNT } from './following-widget'
import { FollowSort, SortDirection } from '@/src/services/wayd-api'
import { MAX_ROWS } from '../widget-settings'
import { renderReportingWidget } from '../widget-display-test-utils'

vi.mock(
  import('@/src/store/features/common/follows-api'),
  async (importOriginal) => ({
    ...(await importOriginal()),
    useGetMyFollowsInfiniteQuery: vi.fn(),
  }),
)

import { useGetMyFollowsInfiniteQuery } from '@/src/store/features/common/follows-api'

const mockFollows = useGetMyFollowsInfiniteQuery as Mock

const followed = (n: number) => ({
  ownerType: 'ppm.project',
  id: `id-${n}`,
  key: `P${n}`,
  name: `Project ${n}`,
  statusName: 'Active',
  lifecycle: 'Active',
})

describe('FollowingWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('says how to follow something when nothing is followed', () => {
    // Arrange
    mockFollows.mockReturnValue({
      data: { pages: [{ items: [] }] },
      isLoading: false,
      isError: false,
    })

    // Act
    render(<FollowingWidget title="Following" />)

    // Assert
    expect(
      screen.getByText(/You are not following anything/),
    ).toBeInTheDocument()
  })

  it('lists the most recently changed records, linking to each, up to the limit', () => {
    // Arrange
    const items = Array.from({ length: FOLLOWING_COUNT + 2 }, (_, i) =>
      followed(i + 1),
    )
    mockFollows.mockReturnValue({
      data: { pages: [{ items, totalCount: 23 }] },
      isLoading: false,
      isError: false,
    })

    // Act
    render(<FollowingWidget title="Following" />)

    // Assert
    expect(screen.getByRole('link', { name: /^Project 1\b/ })).toHaveAttribute(
      'href',
      '/ppm/projects/P1',
    )
    expect(screen.getAllByRole('listitem')).toHaveLength(FOLLOWING_COUNT)
    expect(screen.getByText('And 15 more.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/account/following',
    )
    expect(mockFollows).toHaveBeenCalledWith({
      sort: FollowSort.LastChanged,
      direction: SortDirection.Descending,
      pageSize: MAX_ROWS,
    })
  })

  it('lists as many as its rows setting says, from the same request', () => {
    // Arrange
    const items = Array.from({ length: MAX_ROWS }, (_, i) => followed(i + 1))
    mockFollows.mockReturnValue({
      data: { pages: [{ items, totalCount: 23 }] },
      isLoading: false,
      isError: false,
    })

    // Act
    render(<FollowingWidget title="Following" settings={{ rows: '3' }} />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
    expect(mockFollows).toHaveBeenCalledWith(
      expect.objectContaining({ pageSize: MAX_ROWS }),
    )
  })

  it('says the list failed to load rather than that nothing is followed', () => {
    // Arrange
    mockFollows.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<FollowingWidget title="Following" />)

    // Assert
    expect(
      screen.getByText('Could not load what you follow.'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/not following/)).not.toBeInTheDocument()
  })

  it.each([
    [
      'loading',
      { data: undefined, isLoading: true, isError: false },
      'loading',
    ],
    ['failed', { data: undefined, isLoading: false, isError: true }, 'failed'],
    [
      'empty when nothing is followed',
      { data: { pages: [{ items: [] }] }, isLoading: false, isError: false },
      'empty',
    ],
    [
      'empty when every followed record has no page to link to',
      {
        data: {
          pages: [
            {
              items: [{ ...followed(1), ownerType: 'unknown.type' }],
              totalCount: 1,
            },
          ],
        },
        isLoading: false,
        isError: false,
      },
      'empty',
    ],
    [
      'rows when a followed record can be linked',
      {
        data: { pages: [{ items: [followed(1)], totalCount: 1 }] },
        isLoading: false,
        isError: false,
      },
      'rows',
    ],
  ])('reports %s to the page', (_name, query, expected) => {
    // Arrange
    mockFollows.mockReturnValue(query)

    // Act
    const { reportedState } = renderReportingWidget(
      <FollowingWidget title="Following" />,
    )

    // Assert
    expect(reportedState()).toBe(expected)
  })
})
