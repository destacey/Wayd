import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import {
  NotificationDto,
  NotificationEvent,
  NotificationReason,
} from '@/src/services/api/wayd.schemas'
import MentionsWidget, {
  MentionReasons,
  MENTIONS_COUNT,
  unreadFirst,
  UnreadMentionsTile,
} from './mentions-widget'
import { tileValueStyle } from '../attention-tile-test-utils'
import { MAX_ROWS } from '../widget-settings'
import { renderReportingWidget } from '../widget-display-test-utils'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/components/common/notifications/notification-list', () => ({
  default: function MockNotificationList({
    notifications,
    entryHref,
  }: {
    notifications: NotificationDto[]
    entryHref: (n: NotificationDto) => string | null
  }) {
    return (
      <ul>
        {notifications.map((n) => (
          <li key={n.record.id}>
            <a href={entryHref(n) ?? undefined}>{n.record.name}</a>
          </li>
        ))}
      </ul>
    )
  },
}))

vi.mock('@/src/store/features/common/notifications-api', () => ({
  useGetMyNotificationsInfiniteQuery: vi.fn(),
  useGetUnreadNotificationCountQuery: vi.fn(),
}))

import {
  useGetMyNotificationsInfiniteQuery,
  useGetUnreadNotificationCountQuery,
} from '@/src/store/features/common/notifications-api'

const mockNotifications = useGetMyNotificationsInfiniteQuery as Mock
const mockUnread = useGetUnreadNotificationCountQuery as Mock

const entry = (
  name: string,
  overrides: Partial<NotificationDto> = {},
): NotificationDto =>
  ({
    record: {
      ownerType: 'ppm.project',
      id: `id-${name}`,
      key: name,
      name,
      statusName: 'Active',
    },
    event: NotificationEvent.MessagePosted,
    eventId: `message-${name}`,
    isUnread: false,
    unreadCount: 0,
    count: 1,
    reasons: [NotificationReason.Mentioned],
    ...overrides,
  }) as NotificationDto

/** Answers the unread request and the request for every entry. */
const answer = (unread: NotificationDto[], all: NotificationDto[]) =>
  mockNotifications.mockImplementation((args: { unreadOnly?: boolean }) => ({
    data: { pages: [{ items: args.unreadOnly ? unread : all }] },
    isLoading: false,
    isError: false,
  }))

describe('MentionsWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('asks for mentions and thread replies, unread and all', () => {
    // Arrange
    answer([], [])

    // Act
    render(<MentionsWidget title="Mentions" />)

    // Assert
    const reasons = [NotificationReason.Mentioned, NotificationReason.Thread]
    expect(mockNotifications).toHaveBeenCalledWith({
      pageSize: MAX_ROWS,
      unreadOnly: true,
      reasons,
    })
    expect(mockNotifications).toHaveBeenCalledWith(
      { pageSize: MAX_ROWS, reasons },
      { skip: false },
    )
  })

  it('asks for read entries only once the unread ones leave rows to fill', () => {
    // Arrange — five unread fill the default five rows.
    const unread = Array.from({ length: MENTIONS_COUNT }, (_, i) =>
      entry(`Unread ${i}`, { isUnread: true, unreadCount: 1 }),
    )
    answer(unread, [entry('Read')])

    // Act
    render(<MentionsWidget title="Mentions" />)

    // Assert
    expect(mockNotifications).toHaveBeenCalledWith(
      { pageSize: MAX_ROWS, reasons: MentionReasons },
      { skip: true },
    )
    expect(screen.queryByText('Read')).toBeNull()
  })

  it('says how it fills when nobody has asked for the caller', () => {
    // Arrange
    answer([], [])

    // Act
    render(<MentionsWidget title="Mentions" />)

    // Assert
    expect(screen.getByText(/Nobody has mentioned you/)).toBeInTheDocument()
  })

  it('lists unread conversations before read ones, each once', () => {
    // Arrange
    const apollo = entry('Apollo', { isUnread: true, unreadCount: 1 })
    const borealis = entry('Borealis')
    answer([apollo], [borealis, apollo])

    // Act
    render(<MentionsWidget title="Mentions" />)

    // Assert
    expect(screen.getAllByRole('listitem').map((li) => li.textContent)).toEqual(
      ['Apollo', 'Borealis'],
    )
  })

  it('opens the message an entry is about in its record discussion', () => {
    // Arrange
    answer([], [entry('Apollo')])

    // Act
    render(<MentionsWidget title="Mentions" />)

    // Assert
    expect(screen.getByRole('link', { name: 'Apollo' })).toHaveAttribute(
      'href',
      '/ppm/projects/Apollo?section=discussion&message=message-Apollo',
    )
  })

  it('opens an internal note an entry is about, as it does a message', () => {
    // Arrange
    answer(
      [],
      [
        entry('Intake', {
          record: {
            ownerType: 'demand.intake-request',
            id: 'id-intake',
            key: '15',
            name: 'Intake',
            statusName: 'Open',
          },
          event: NotificationEvent.InternalNotePosted,
          eventId: 'note-1',
        } as Partial<NotificationDto>),
      ],
    )

    // Act
    render(<MentionsWidget title="Mentions" />)

    // Assert
    expect(screen.getByRole('link', { name: 'Intake' })).toHaveAttribute(
      'href',
      '/demand-management/requests/15?section=discussion&message=note-1',
    )
  })

  it('opens the conversation alone for an entry about no message', () => {
    // Arrange
    answer([], [entry('Apollo', { event: NotificationEvent.StatusChanged })])

    // Act
    render(<MentionsWidget title="Mentions" />)

    // Assert
    expect(screen.getByRole('link', { name: 'Apollo' })).toHaveAttribute(
      'href',
      '/ppm/projects/Apollo?section=discussion',
    )
  })

  it.each([
    [undefined, MENTIONS_COUNT],
    [{ rows: '3' }, 3],
  ])('lists as many as its rows setting says', (settings, expected) => {
    // Arrange
    const items = Array.from({ length: 20 }, (_, i) => entry(`Record ${i}`))
    answer([], items)

    // Act
    render(<MentionsWidget title="Mentions" settings={settings} />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(expected)
  })

  it.each([
    ['empty', [] as NotificationDto[], 'empty'],
    ['rows', [entry('Apollo')], 'rows'],
  ])('reports %s to the page', (_name, items, expected) => {
    // Arrange
    answer([], items)

    // Act
    const { reportedState } = renderReportingWidget(
      <MentionsWidget title="Mentions" />,
    )

    // Assert
    expect(reportedState()).toBe(expected)
  })
})

describe('unreadFirst', () => {
  it('leaves out an entry the unread list already holds, even when the other list calls it read', () => {
    // Arrange — read between the two requests.
    const apollo = entry('Apollo', { isUnread: true })
    const apolloRead = entry('Apollo')

    // Act
    const result = unreadFirst([apollo], [apolloRead])

    // Assert
    expect(result).toEqual([apollo])
  })
})

describe('UnreadMentionsTile', () => {
  it('counts unread mentions in the warning colour', () => {
    // Arrange
    mockUnread.mockReturnValue({
      data: { count: 2, isCapped: false },
      isLoading: false,
      isError: false,
    })

    // Act
    const { container } = render(<UnreadMentionsTile title="Unread mentions" />)

    // Assert
    expect(mockUnread).toHaveBeenCalledWith({
      reasons: [NotificationReason.Mentioned, NotificationReason.Thread],
    })
    expect(
      screen.getByRole('button', { name: 'Unread mentions: 2' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-warning)')
  })
})
