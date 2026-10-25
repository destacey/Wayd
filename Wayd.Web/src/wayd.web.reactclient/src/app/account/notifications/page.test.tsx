import { fireEvent, render, screen } from '@testing-library/react'
import { NotificationDto } from '@/src/services/wayd-api'
import { NoNotificationsMessage } from '@/src/components/common/notifications/notification-list'
import {
  buildNotification,
  buildNotifiedRecord,
} from '@/src/components/common/notifications/notification-test-data'
import NotificationsPage from './page'

jest.mock('@/src/components/hoc', () => ({
  requireFeatureFlag: (Page: React.ComponentType) => Page,
}))

const mockUseAuth = jest.fn()
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => mockUseAuth(),
}))

const mockUseList = jest.fn()
const mockUseCount = jest.fn()
const mockMarkAllRead = jest.fn()
jest.mock('@/src/store/features/common/notifications-api', () => ({
  useGetMyNotificationsInfiniteQuery: (...args: unknown[]) =>
    mockUseList(...args),
  useGetUnreadNotificationCountQuery: (...args: unknown[]) =>
    mockUseCount(...args),
  useMarkAllNotificationsReadMutation: () => [
    mockMarkAllRead,
    { isLoading: false },
  ],
  useMarkRecordNotificationsReadMutation: () => [jest.fn(), {}],
}))

const notification = (id: string, name: string): NotificationDto =>
  buildNotification({ record: buildNotifiedRecord({ id, key: id, name }) })

const list = (pages: NotificationDto[][], hasNextPage = false) => ({
  data: { pages: pages.map((items) => ({ items })) },
  isLoading: false,
  isError: false,
  hasNextPage,
  isFetchingNextPage: false,
  fetchNextPage: jest.fn(),
})

describe('NotificationsPage', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseAuth.mockReturnValue({ user: { employeeId: 'emp-1' } })
    mockUseCount.mockReturnValue({ data: { count: 1, isCapped: false } })
  })

  it('shows every loaded page, refetched on each visit', () => {
    // Arrange
    mockUseList.mockReturnValue(
      list([[notification('1', 'Apollo')], [notification('2', 'Gemini')]]),
    )

    // Act
    render(<NotificationsPage />)

    // Assert
    expect(mockUseList).toHaveBeenCalledWith(
      { unreadOnly: undefined, pageSize: 25 },
      { refetchOnMountOrArgChange: true },
    )
    expect(screen.getAllByRole('listitem')).toHaveLength(2)
  })

  it('loads the next page on request', () => {
    // Arrange
    const result = list([[notification('1', 'Apollo')]], true)
    mockUseList.mockReturnValue(result)
    render(<NotificationsPage />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Load more' }))

    // Assert
    expect(result.fetchNextPage).toHaveBeenCalledTimes(1)
  })

  it('offers no more when the last page is loaded', () => {
    // Arrange
    mockUseList.mockReturnValue(list([[notification('1', 'Apollo')]]))

    // Act
    render(<NotificationsPage />)

    // Assert
    expect(screen.queryByRole('button', { name: 'Load more' })).toBeNull()
  })

  it('marks everything read', () => {
    // Arrange
    mockUseList.mockReturnValue(list([[notification('1', 'Apollo')]]))
    render(<NotificationsPage />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Mark all read' }))

    // Assert
    expect(mockMarkAllRead).toHaveBeenCalledTimes(1)
  })

  it('offers no mark all read when nothing is unread', () => {
    // Arrange
    mockUseList.mockReturnValue(list([[notification('1', 'Apollo')]]))
    mockUseCount.mockReturnValue({ data: { count: 0, isCapped: false } })

    // Act
    render(<NotificationsPage />)

    // Assert
    expect(screen.getByRole('button', { name: 'Mark all read' })).toBeDisabled()
  })

  it('explains following when nothing has arrived', () => {
    // Arrange
    mockUseList.mockReturnValue(list([[]]))

    // Act
    render(<NotificationsPage />)

    // Assert
    expect(screen.getByText(NoNotificationsMessage)).toBeInTheDocument()
  })

  it('asks for a linked employee, and loads nothing, without one', () => {
    // Arrange
    mockUseAuth.mockReturnValue({ user: { employeeId: undefined } })

    // Act
    render(<NotificationsPage />)

    // Assert
    expect(
      screen.getByText(/Notifications need your account to be linked/),
    ).toBeInTheDocument()
    expect(mockUseList).not.toHaveBeenCalled()
  })
})
