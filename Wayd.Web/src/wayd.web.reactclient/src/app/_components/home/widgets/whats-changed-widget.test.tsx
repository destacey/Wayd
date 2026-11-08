import { render, screen } from '@testing-library/react'
import WhatsChangedWidget, {
  UnreadNotificationsTile,
  WHATS_CHANGED_COUNT,
} from './whats-changed-widget'
import { tileValueStyle } from '../attention-tile-test-utils'

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: jest.fn() }),
}))

jest.mock(
  '@/src/components/common/notifications/notification-list',
  () =>
    function MockNotificationList({
      notifications,
    }: {
      notifications: { record: { name: string } }[]
    }) {
      return (
        <ul>
          {notifications.map((n) => (
            <li key={n.record.name}>{n.record.name}</li>
          ))}
        </ul>
      )
    },
)

jest.mock('@/src/store/features/common/notifications-api', () => ({
  useGetMyNotificationsInfiniteQuery: jest.fn(),
  useGetUnreadNotificationCountQuery: jest.fn(),
}))

import {
  useGetMyNotificationsInfiniteQuery,
  useGetUnreadNotificationCountQuery,
} from '@/src/store/features/common/notifications-api'

const mockNotifications = useGetMyNotificationsInfiniteQuery as jest.Mock
const mockUnread = useGetUnreadNotificationCountQuery as jest.Mock

describe('WhatsChangedWidget', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('says how it fills when nothing has changed', () => {
    // Arrange
    mockNotifications.mockReturnValue({
      data: { pages: [{ items: [] }] },
      isLoading: false,
      isError: false,
    })

    // Act
    render(<WhatsChangedWidget title="What's changed" />)

    // Assert
    expect(screen.getByText(/Nothing has changed yet/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/account/notifications',
    )
  })

  it('lists the latest notifications from the first page', () => {
    // Arrange
    mockNotifications.mockReturnValue({
      data: {
        pages: [{ items: [{ record: { name: 'Apollo' } }] }],
      },
      isLoading: false,
      isError: false,
    })

    // Act
    render(<WhatsChangedWidget title="What's changed" />)

    // Assert
    expect(screen.getByText('Apollo')).toBeInTheDocument()
    expect(screen.queryByText(/Nothing has changed/)).not.toBeInTheDocument()
    expect(mockNotifications).toHaveBeenCalledWith({
      pageSize: WHATS_CHANGED_COUNT,
    })
  })
})

describe('UnreadNotificationsTile', () => {
  it('counts unread records in the warning colour', () => {
    // Arrange
    mockUnread.mockReturnValue({
      data: { count: 3, isCapped: false },
      isLoading: false,
      isError: false,
    })

    // Act
    const { container } = render(<UnreadNotificationsTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Unread notifications: 3' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-warning)')
  })

  it('leaves nothing unread uncoloured', () => {
    // Arrange
    mockUnread.mockReturnValue({
      data: { count: 0, isCapped: false },
      isLoading: false,
      isError: false,
    })

    // Act
    const { container } = render(<UnreadNotificationsTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Unread notifications: 0' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).not.toContain('var(--ant-color')
  })

  it('shows a capped count as 99+', () => {
    // Arrange
    mockUnread.mockReturnValue({
      data: { count: 99, isCapped: true },
      isLoading: false,
      isError: false,
    })

    // Act
    render(<UnreadNotificationsTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Unread notifications: 99+' }),
    ).toBeInTheDocument()
  })
})
