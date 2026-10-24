import { act, fireEvent, render, screen } from '@testing-library/react'
import { FollowedRecordDto, FollowGroupDto } from '@/src/services/wayd-api'
import FollowingGroup from './following-group'

const mockMessageError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockMessageError }),
}))

const mockUseGetMyFollowsQuery = jest.fn()
const mockUnfollow = jest.fn()
jest.mock('@/src/store/features/common/follows-api', () => ({
  ...jest.requireActual('@/src/store/features/common/follows-api'),
  useGetMyFollowsQuery: (...args: unknown[]) =>
    mockUseGetMyFollowsQuery(...args),
  useUnfollowMutation: () => [mockUnfollow, { isLoading: false }],
}))

const project = (key: string, name: string): FollowedRecordDto => ({
  ownerType: 'ppm.project',
  id: `id-${key}`,
  key,
  name,
  followedOn: new Date('2026-10-23T09:30:00Z'),
})

const group = (
  items: FollowedRecordDto[],
  totalCount: number,
  pageNumber = 1,
): FollowGroupDto => ({
  ownerType: 'ppm.project',
  displayName: 'Project',
  follows: {
    items,
    totalCount,
    pageNumber,
    pageSize: 2,
    totalPages: Math.ceil(totalCount / 2),
    hasPreviousPage: pageNumber > 1,
    hasNextPage: pageNumber * 2 < totalCount,
  },
})

describe('FollowingGroup', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseGetMyFollowsQuery.mockReturnValue({
      currentData: undefined,
      isFetching: false,
    })
    mockUnfollow.mockResolvedValue({ data: undefined })
  })

  it('shows the first page from the overview, linking each record to its page', () => {
    render(
      <FollowingGroup
        group={group(
          [project('APOLLO', 'Moon landing'), project('GEMINI', 'Docking')],
          3,
        )}
        pageSize={2}
      />,
    )

    expect(screen.getByText('Project')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'APOLLO - Moon landing' }),
    ).toHaveAttribute('href', '/ppm/projects/APOLLO')
    expect(
      screen.getByRole('link', { name: 'GEMINI - Docking' }),
    ).toBeInTheDocument()
    expect(mockUseGetMyFollowsQuery).toHaveBeenCalledWith(
      { ownerType: 'ppm.project', page: 1, pageSize: 2 },
      { skip: true },
    )
  })

  it('fetches a later page for this kind of record alone', async () => {
    mockUseGetMyFollowsQuery.mockImplementation((request: { page: number }) =>
      request.page === 2
        ? {
            currentData: [group([project('MERCURY', 'Orbit')], 3, 2)],
            isFetching: false,
          }
        : { currentData: undefined, isFetching: false },
    )

    render(
      <FollowingGroup
        group={group(
          [project('APOLLO', 'Moon landing'), project('GEMINI', 'Docking')],
          3,
        )}
        pageSize={2}
      />,
    )

    await act(async () => {
      fireEvent.click(screen.getByTitle('2'))
    })

    expect(mockUseGetMyFollowsQuery).toHaveBeenLastCalledWith(
      { ownerType: 'ppm.project', page: 2, pageSize: 2 },
      { skip: false },
    )
    expect(
      screen.getByRole('link', { name: 'MERCURY - Orbit' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'APOLLO - Moon landing' }),
    ).not.toBeInTheDocument()
  })

  it('shows no rows from the first page while a later page loads', async () => {
    mockUseGetMyFollowsQuery.mockImplementation((request: { page: number }) =>
      request.page === 2
        ? { currentData: undefined, isFetching: true }
        : { currentData: undefined, isFetching: false },
    )

    render(
      <FollowingGroup
        group={group(
          [project('APOLLO', 'Moon landing'), project('GEMINI', 'Docking')],
          3,
        )}
        pageSize={2}
      />,
    )

    await act(async () => {
      fireEvent.click(screen.getByTitle('2'))
    })

    expect(
      screen.queryByRole('link', { name: 'APOLLO - Moon landing' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /^Unfollow/ }),
    ).not.toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
  })

  it('steps back a page after unfollowing the last record on a later page', async () => {
    mockUseGetMyFollowsQuery.mockImplementation((request: { page: number }) =>
      request.page === 2
        ? {
            currentData: [group([project('MERCURY', 'Orbit')], 3, 2)],
            isFetching: false,
          }
        : { currentData: undefined, isFetching: false },
    )

    render(
      <FollowingGroup
        group={group(
          [project('APOLLO', 'Moon landing'), project('GEMINI', 'Docking')],
          3,
        )}
        pageSize={2}
      />,
    )
    await act(async () => {
      fireEvent.click(screen.getByTitle('2'))
    })

    await act(async () => {
      fireEvent.click(
        screen.getByRole('button', { name: 'Unfollow MERCURY - Orbit' }),
      )
    })

    expect(mockUnfollow).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      ownerId: 'id-MERCURY',
    })
    expect(
      screen.getByRole('link', { name: 'APOLLO - Moon landing' }),
    ).toBeInTheDocument()
  })

  it('does not page when everything fits on one page', () => {
    render(
      <FollowingGroup
        group={group([project('APOLLO', 'Moon landing')], 1)}
        pageSize={2}
      />,
    )

    expect(screen.queryByTitle('2')).not.toBeInTheDocument()
  })

  it('unfollows a record from the list', async () => {
    const apollo = project('APOLLO', 'Moon landing')
    render(<FollowingGroup group={group([apollo], 1)} pageSize={2} />)

    await act(async () => {
      fireEvent.click(
        screen.getByRole('button', { name: 'Unfollow APOLLO - Moon landing' }),
      )
    })

    expect(mockUnfollow).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      ownerId: apollo.id,
    })
    expect(mockMessageError).not.toHaveBeenCalled()
  })

  it('explains a failed unfollow', async () => {
    mockUnfollow.mockResolvedValue({ error: { status: 400, detail: 'Nope.' } })
    render(
      <FollowingGroup
        group={group([project('APOLLO', 'Moon landing')], 1)}
        pageSize={2}
      />,
    )

    await act(async () => {
      fireEvent.click(
        screen.getByRole('button', { name: 'Unfollow APOLLO - Moon landing' }),
      )
    })

    expect(mockMessageError).toHaveBeenCalledWith('Nope.')
  })
})
