import { fireEvent, render, screen } from '@testing-library/react'
import { NotificationDto } from '@/src/services/wayd-api'
import {
  NoNotificationsMessage,
  NothingUnreadMessage,
} from '@/src/components/common/notifications/notification-list'
import {
  buildNotification,
  buildReadNotification,
} from '@/src/components/common/notifications/notification-test-data'
import NotificationPanel from './notification-panel'

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

jest.mock('@/src/store/features/common/notification-mutes-api', () => ({
  useMuteRecordMutation: () => [jest.fn(), {}],
  useUnmuteRecordMutation: () => [jest.fn(), {}],
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: jest.fn(),
    error: jest.fn(),
    destroy: jest.fn(),
  }),
}))

const arrange = (items: NotificationDto[], unreadCount = 0) => {
  mockUseList.mockReturnValue({
    data: { pages: [{ items }] },
    isLoading: false,
    isError: false,
  })
  mockUseCount.mockReturnValue({
    data: { count: unreadCount, isCapped: false },
  })
}

describe('NotificationPanel', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    // jest.setup's stub has no backing store; each test says what was stored.
    jest.mocked(window.localStorage.getItem).mockReset()
  })

  it('asks for the newest ten entries and the count, both refetched on every opening', () => {
    // Arrange
    arrange([])

    // Act
    render(<NotificationPanel onClose={jest.fn()} />)

    // Assert
    expect(mockUseList).toHaveBeenCalledWith(
      { unreadOnly: undefined, pageSize: 10 },
      { refetchOnMountOrArgChange: true },
    )
    expect(mockUseCount).toHaveBeenCalledWith(undefined, {
      refetchOnMountOrArgChange: true,
    })
    expect(screen.getByText(NoNotificationsMessage)).toBeInTheDocument()
  })

  it('is a dialog that takes focus when it opens', () => {
    // Arrange
    arrange([])

    // Act
    render(<NotificationPanel onClose={jest.fn()} />)

    // Assert
    expect(screen.getByRole('dialog', { name: 'Notifications' })).toHaveFocus()
  })

  it('closes on Escape and hands focus back to the bell', () => {
    // Arrange
    arrange([])
    const onClose = jest.fn()
    render(<NotificationPanel onClose={onClose} />)

    // Act
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' })

    // Assert
    expect(onClose).toHaveBeenCalledWith({ returnFocus: true })
  })

  it('leaves naming and focus to the drawer it sits in', () => {
    // Arrange
    arrange([])

    // Act
    render(<NotificationPanel onClose={jest.fn()} inDrawer />)

    // Assert
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.queryByText('Notifications')).toBeNull()
  })

  it('shows only unread entries on the Unread tab', () => {
    // Arrange
    arrange([])
    render(<NotificationPanel onClose={jest.fn()} />)

    // Act
    fireEvent.click(screen.getByRole('radio', { name: 'Unread' }))

    // Assert
    expect(mockUseList).toHaveBeenLastCalledWith(
      { unreadOnly: true, pageSize: 10 },
      { refetchOnMountOrArgChange: true },
    )
    expect(screen.getByText(NothingUnreadMessage)).toBeInTheDocument()
  })

  it('remembers the tab the reader chose', () => {
    // Arrange
    arrange([])
    render(<NotificationPanel onClose={jest.fn()} />)

    // Act
    fireEvent.click(screen.getByRole('radio', { name: 'Unread' }))

    // Assert
    expect(window.localStorage.setItem).toHaveBeenLastCalledWith(
      'wayd.notifications.panelView',
      '"unread"',
    )
  })

  it('opens on the tab the reader last chose', () => {
    // Arrange
    arrange([])
    jest
      .mocked(window.localStorage.getItem)
      .mockImplementation((key) =>
        key === 'wayd.notifications.panelView' ? '"unread"' : null,
      )

    // Act
    render(<NotificationPanel onClose={jest.fn()} />)

    // Assert
    expect(screen.getByRole('radio', { name: 'Unread' })).toBeChecked()
    expect(mockUseList).toHaveBeenCalledWith(
      { unreadOnly: true, pageSize: 10 },
      { refetchOnMountOrArgChange: true },
    )
  })

  it('marks everything read', () => {
    // Arrange
    arrange([buildNotification()], 1)
    render(<NotificationPanel onClose={jest.fn()} />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Mark all read' }))

    // Assert
    expect(mockMarkAllRead).toHaveBeenCalledTimes(1)
  })

  it('offers mark all read for unread entries older than those shown', () => {
    // Arrange
    arrange([buildReadNotification()], 1)

    // Act
    render(<NotificationPanel onClose={jest.fn()} />)

    // Assert
    expect(screen.getByRole('button', { name: 'Mark all read' })).toBeEnabled()
  })

  it('offers no mark all read when nothing is unread', () => {
    // Arrange
    arrange([buildReadNotification()], 0)

    // Act
    render(<NotificationPanel onClose={jest.fn()} />)

    // Assert
    expect(screen.getByRole('button', { name: 'Mark all read' })).toBeDisabled()
  })

  it('links to the page and closes when it is followed', () => {
    // Arrange
    arrange([])
    const onClose = jest.fn()
    render(<NotificationPanel onClose={onClose} />)
    const link = screen.getByRole('link', { name: 'See all notifications' })

    // Act
    fireEvent.click(link)

    // Assert
    expect(link).toHaveAttribute('href', '/account/notifications')
    expect(onClose).toHaveBeenCalledTimes(1)
  })
})
