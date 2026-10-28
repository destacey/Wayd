import { act, fireEvent, render, screen, within } from '@testing-library/react'
import {
  LifecycleCategory,
  MutedRecordDto,
  NotificationReason,
} from '@/src/services/wayd-api'
import MutedNotifications, {
  NoMutedRecordsMessage,
} from './muted-notifications'

const mockMessageError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockMessageError }),
}))

const mockUseReasons = jest.fn()
const mockUseRecords = jest.fn()
const mockMuteReason = jest.fn()
const mockUnmuteReason = jest.fn()
const mockUnmuteRecord = jest.fn()
jest.mock('@/src/store/features/common/notification-mutes-api', () => ({
  useGetReasonMutesQuery: (...args: unknown[]) => mockUseReasons(...args),
  useGetMutedRecordsInfiniteQuery: (...args: unknown[]) =>
    mockUseRecords(...args),
  useMuteReasonMutation: () => [mockMuteReason, { isLoading: false }],
  useUnmuteReasonMutation: () => [mockUnmuteReason, { isLoading: false }],
  useUnmuteRecordMutation: () => [mockUnmuteRecord, { isLoading: false }],
}))

const mutedRecord = (
  overrides: Partial<MutedRecordDto> = {},
): MutedRecordDto => ({
  ownerType: 'ppm.project',
  id: 'p-1',
  key: '42',
  name: 'Apollo',
  statusName: 'Active',
  lifecycle: LifecycleCategory.Active,
  isFollowing: false,
  mutedOn: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000),
  ...overrides,
})

const records = (items: MutedRecordDto[], hasNextPage = false) => ({
  data: { pages: [{ items }] },
  isLoading: false,
  isError: false,
  hasNextPage,
  isFetchingNextPage: false,
  fetchNextPage: jest.fn(),
})

describe('MutedNotifications', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseReasons.mockReturnValue({
      data: [
        { reason: NotificationReason.Following, isMuted: false },
        {
          reason: NotificationReason.Role,
          isMuted: true,
          mutedOn: new Date(),
        },
      ],
      isLoading: false,
      isError: false,
    })
    mockUseRecords.mockReturnValue(records([]))
    mockMuteReason.mockResolvedValue({ data: null })
    mockUnmuteReason.mockResolvedValue({ data: null })
    mockUnmuteRecord.mockResolvedValue({ data: null })
  })

  it('reads both lists fresh on every visit', () => {
    // Act
    render(<MutedNotifications />)

    // Assert
    expect(mockUseReasons).toHaveBeenCalledWith(undefined, {
      refetchOnMountOrArgChange: true,
    })
    expect(mockUseRecords).toHaveBeenCalledWith(undefined, {
      refetchOnMountOrArgChange: true,
    })
  })

  it('shows each reason with whether it is muted', () => {
    // Act
    render(<MutedNotifications />)

    // Assert
    expect(
      screen.getByRole('switch', { name: 'Records you follow' }),
    ).not.toBeChecked()
    expect(
      screen.getByRole('switch', { name: 'Records you work on' }),
    ).toBeChecked()
  })

  it('leaves out a reason it has no words for', () => {
    // Arrange
    mockUseReasons.mockReturnValue({
      data: [{ reason: NotificationReason.Mentioned, isMuted: false }],
      isLoading: false,
      isError: false,
    })

    // Act
    render(<MutedNotifications />)

    // Assert
    expect(screen.queryByRole('switch')).toBeNull()
  })

  it('mutes and unmutes a reason from its switch', async () => {
    // Arrange
    render(<MutedNotifications />)

    // Act
    await act(async () => {
      fireEvent.click(
        screen.getByRole('switch', { name: 'Records you follow' }),
      )
      fireEvent.click(
        screen.getByRole('switch', { name: 'Records you work on' }),
      )
    })

    // Assert
    expect(mockMuteReason).toHaveBeenCalledWith(NotificationReason.Following)
    expect(mockUnmuteReason).toHaveBeenCalledWith(
      NotificationReason.Role,
    )
  })

  it('lists muted records with when they were muted and whether they are still followed', () => {
    // Arrange
    mockUseRecords.mockReturnValue(
      records([
        mutedRecord(),
        mutedRecord({
          ownerType: 'ppm.program',
          id: 'g-1',
          key: '7',
          name: 'Gemini',
          isFollowing: true,
        }),
      ]),
    )

    // Act
    render(<MutedNotifications />)

    // Assert
    const list = screen.getByRole('list', { name: 'Muted records' })
    const rows = within(list).getAllByRole('listitem')
    expect(rows).toHaveLength(2)
    expect(
      within(rows[0]).getByRole('link', { name: 'Apollo' }),
    ).toHaveAttribute('href', '/ppm/projects/42')
    expect(rows[0]).toHaveTextContent('Project 42 · Muted 3 days ago')
    expect(rows[0]).not.toHaveTextContent('You still follow this')
    expect(rows[1]).toHaveTextContent('You still follow this')
  })

  it('unmutes a record', async () => {
    // Arrange
    mockUseRecords.mockReturnValue(records([mutedRecord()]))
    render(<MutedNotifications />)

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Unmute Apollo' }))
    })

    // Assert
    expect(mockUnmuteRecord).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      ownerId: 'p-1',
    })
  })

  it('says why an unmute failed', async () => {
    // Arrange
    mockUseRecords.mockReturnValue(records([mutedRecord()]))
    mockUnmuteRecord.mockResolvedValue({
      error: { status: 500, detail: 'The server is down.' },
    })
    render(<MutedNotifications />)

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Unmute Apollo' }))
    })

    // Assert
    expect(mockMessageError).toHaveBeenCalledWith('The server is down.')
  })

  it('loads more muted records on request', () => {
    // Arrange
    const result = records([mutedRecord()], true)
    mockUseRecords.mockReturnValue(result)
    render(<MutedNotifications />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Load more' }))

    // Assert
    expect(result.fetchNextPage).toHaveBeenCalledTimes(1)
  })

  it('explains how to mute when nothing is muted', () => {
    // Act
    render(<MutedNotifications />)

    // Assert
    expect(screen.getByText(NoMutedRecordsMessage)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Load more' })).toBeNull()
  })
})
