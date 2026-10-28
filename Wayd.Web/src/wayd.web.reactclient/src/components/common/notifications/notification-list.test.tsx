import { act, fireEvent, render, screen, within } from '@testing-library/react'
import {
  EventActorKind,
  NotificationDto,
  NotificationEvent,
  NotificationReason,
} from '@/src/services/wayd-api'
import NotificationList, { NoNotificationsMessage } from './notification-list'
import {
  buildNotification,
  buildNotifiedRecord,
  buildReadNotification,
} from './notification-test-data'

const mockMarkRecordRead = jest.fn()
jest.mock('@/src/store/features/common/notifications-api', () => ({
  useMarkRecordNotificationsReadMutation: () => [mockMarkRecordRead, {}],
}))

const mockMuteRecord = jest.fn()
const mockMuting: { current: object } = { current: {} }
const mockUnmuteRecord = jest.fn()
jest.mock('@/src/store/features/common/notification-mutes-api', () => ({
  useMuteRecordMutation: () => [mockMuteRecord, mockMuting.current],
  useUnmuteRecordMutation: () => [mockUnmuteRecord, {}],
}))

const mockMessageSuccess = jest.fn()
const mockMessageInfo = jest.fn()
const mockMessageError = jest.fn()
const mockMessageDestroy = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: mockMessageSuccess,
    info: mockMessageInfo,
    error: mockMessageError,
    destroy: mockMessageDestroy,
  }),
}))

const renderList = (
  notifications: NotificationDto[],
  { onNavigate = jest.fn(), isLoading = false, isError = false } = {},
) =>
  render(
    <NotificationList
      notifications={notifications}
      isLoading={isLoading}
      isError={isError}
      emptyMessage={NoNotificationsMessage}
      onNavigate={onNavigate}
    />,
  )

describe('NotificationList', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it.each([
    [NotificationEvent.Created, 'Ada Lovelace created Apollo'],
    [
      NotificationEvent.StatusChanged,
      'Ada Lovelace changed the status of Apollo',
    ],
    [
      NotificationEvent.LifecycleChanged,
      'Ada Lovelace changed the lifecycle of Apollo',
    ],
    [
      NotificationEvent.HealthReported,
      'Ada Lovelace reported health on Apollo',
    ],
    [
      NotificationEvent.TimelineChanged,
      'Ada Lovelace changed the dates of Apollo',
    ],
    [
      NotificationEvent.RolesChanged,
      'Ada Lovelace changed the people on Apollo',
    ],
    [NotificationEvent.Reparented, 'Ada Lovelace moved Apollo'],
    [NotificationEvent.ScoreRecorded, 'Ada Lovelace scored Apollo'],
  ])('words a %s change as a sentence', (event, sentence) => {
    // Act
    renderList([buildReadNotification({ event })])

    // Assert
    expect(screen.getByRole('link')).toHaveTextContent(sentence)
  })

  it('names a change with no linked employee "Someone"', () => {
    // Act
    renderList([
      buildReadNotification({
        actor: undefined,
        actorKind: EventActorKind.User,
      }),
    ])

    // Assert
    expect(screen.getByRole('link')).toHaveTextContent(
      'Someone changed the status of Apollo',
    )
  })

  it('explains following when there is nothing to show', () => {
    // Act
    renderList([])

    // Assert
    expect(screen.getByText(NoNotificationsMessage)).toBeInTheDocument()
    expect(screen.queryByRole('list')).toBeNull()
  })

  it('shows a spinner while loading, not the empty state', () => {
    // Act
    const { container } = renderList([], { isLoading: true })

    // Assert
    expect(container.querySelector('.ant-spin')).not.toBeNull()
    expect(screen.queryByText(NoNotificationsMessage)).toBeNull()
  })

  it('reports a failed load, not the empty state', () => {
    // Act
    renderList([], { isError: true })

    // Assert
    expect(
      screen.getByText('Could not load your notifications.'),
    ).toBeInTheDocument()
    expect(screen.queryByText(NoNotificationsMessage)).toBeNull()
  })

  it('says what happened, where, and why the reader was told', () => {
    // Arrange
    const entry = buildNotification({
      count: 3,
      unreadCount: 2,
      reasons: [NotificationReason.Following, NotificationReason.Participant],
    })

    // Act
    renderList([entry])

    // Assert
    const link = screen.getByRole('link', {
      name: /^Unread: Ada Lovelace changed the status of Apollo and 2 other updates/,
    })
    expect(link).toHaveAttribute('href', '/ppm/projects/APOLLO')
    expect(link).toHaveTextContent('Project APOLLO')
    expect(link).toHaveTextContent('You follow and take part in this')
    const item = screen.getByRole('listitem')
    expect(within(item).getByText('Active')).toBeInTheDocument()
    expect(item).toHaveTextContent('2 unreadActive')
  })

  it('shows no unread number when one change is unread', () => {
    // Act
    renderList([buildNotification({ count: 3, unreadCount: 1 })])

    // Assert
    expect(screen.getByRole('listitem')).not.toHaveTextContent(/\d unread/)
  })

  it('does not call a read entry unread', () => {
    // Act
    renderList([buildReadNotification()])

    // Assert
    expect(screen.getByRole('link').textContent).not.toMatch(/^Unread/)
  })

  it('marks the record read and closes the panel when an unread entry is opened', () => {
    // Arrange
    const onNavigate = jest.fn()
    renderList([buildNotification()], { onNavigate })

    // Act
    fireEvent.click(screen.getByRole('link'))

    // Assert
    expect(mockMarkRecordRead).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      ownerId: 'p-1',
    })
    expect(onNavigate).toHaveBeenCalledTimes(1)
  })

  it('marks nothing when a read entry is opened', () => {
    // Arrange
    const onNavigate = jest.fn()
    renderList([buildReadNotification()], { onNavigate })

    // Act
    fireEvent.click(screen.getByRole('link'))

    // Assert
    expect(mockMarkRecordRead).not.toHaveBeenCalled()
    expect(onNavigate).toHaveBeenCalledTimes(1)
  })

  it('marks an entry read without opening it', () => {
    // Arrange
    const onNavigate = jest.fn()
    renderList([buildNotification()], { onNavigate })

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Mark Apollo read' }))

    // Assert
    expect(mockMarkRecordRead).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      ownerId: 'p-1',
    })
    expect(onNavigate).not.toHaveBeenCalled()
  })

  it('mutes an entry without opening it, and undoes the mute on request', async () => {
    // Arrange
    mockMuteRecord.mockResolvedValue({ data: { wasAlreadyMuted: false } })
    mockUnmuteRecord.mockResolvedValue({ data: null })
    const onNavigate = jest.fn()
    renderList([buildNotification()], { onNavigate })

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Mute Apollo' }))
    })

    // Assert
    const target = { ownerType: 'ppm.project', ownerId: 'p-1' }
    expect(mockMuteRecord).toHaveBeenCalledWith(target)
    expect(onNavigate).not.toHaveBeenCalled()
    expect(mockMessageSuccess).toHaveBeenCalledTimes(1)

    // Act: Undo from the confirmation
    const { content, key } = mockMessageSuccess.mock.calls[0][0]
    render(content)
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Undo' }))
    })

    // Assert
    expect(mockUnmuteRecord).toHaveBeenCalledWith(target)
    expect(mockMessageDestroy).toHaveBeenCalledWith(key)
  })

  it('keeps the confirmation until dismissed, with focus on Undo, and returns focus on Dismiss', async () => {
    // Arrange
    mockMuteRecord.mockResolvedValue({ data: { wasAlreadyMuted: false } })
    renderList([buildNotification()])
    const muteButton = screen.getByRole('button', { name: 'Mute Apollo' })
    muteButton.focus()

    // Act
    await act(async () => {
      fireEvent.click(muteButton)
    })
    const { content, key, duration } = mockMessageSuccess.mock.calls[0][0]
    const toast = render(content)

    // Assert
    expect(duration).toBe(0)
    expect(
      within(toast.container).getByRole('button', { name: 'Undo' }),
    ).toHaveFocus()

    // Act
    fireEvent.click(
      within(toast.container).getByRole('button', { name: 'Dismiss' }),
    )

    // Assert
    expect(mockMessageDestroy).toHaveBeenCalledWith(key)
    expect(muteButton).toHaveFocus()
    expect(mockUnmuteRecord).not.toHaveBeenCalled()
  })

  it('shows a mute on its way only on that entry', () => {
    // Arrange
    mockMuting.current = {
      isLoading: true,
      originalArgs: { ownerType: 'ppm.project', ownerId: 'p-1' },
    }

    // Act
    renderList([
      buildNotification(),
      buildNotification({
        record: buildNotifiedRecord({ id: 'p-2', key: 'P2', name: 'Gemini' }),
      }),
    ])

    // Assert
    expect(screen.getByRole('button', { name: 'Mute Apollo' })).toHaveClass(
      'ant-btn-loading',
    )
    expect(screen.getByRole('button', { name: 'Mute Gemini' })).not.toHaveClass(
      'ant-btn-loading',
    )
    mockMuting.current = {}
  })

  it('offers no undo when the record was already muted', async () => {
    // Arrange
    mockMuteRecord.mockResolvedValue({ data: { wasAlreadyMuted: true } })
    renderList([buildReadNotification()])

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Mute Apollo' }))
    })

    // Assert
    expect(mockMessageInfo).toHaveBeenCalledWith('Apollo is already muted.')
    expect(mockMessageSuccess).not.toHaveBeenCalled()
    expect(mockUnmuteRecord).not.toHaveBeenCalled()
  })

  it('reports a mute that failed, with nothing to undo', async () => {
    // Arrange
    mockMuteRecord.mockResolvedValue({
      error: { status: 500, detail: 'The server is down.' },
    })
    renderList([buildNotification()])

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Mute Apollo' }))
    })

    // Assert
    expect(mockMessageError).toHaveBeenCalledWith('The server is down.')
    expect(mockMessageSuccess).not.toHaveBeenCalled()
  })

  it('offers mute on a read entry too', () => {
    // Act
    renderList([buildReadNotification()])

    // Assert
    expect(
      screen.getByRole('button', { name: 'Mute Apollo' }),
    ).toBeInTheDocument()
  })

  it('offers no mark-read on a read entry', () => {
    // Act
    renderList([buildReadNotification()])

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Mark Apollo read' }),
    ).toBeNull()
  })

  it('shows an entry with no page for its record without a link', () => {
    // Arrange
    const entry = buildNotification({
      record: buildNotifiedRecord({ ownerType: 'demand.unknown-record' }),
    })

    // Act
    renderList([entry])

    // Assert
    expect(screen.getByRole('listitem')).toHaveTextContent(
      'Ada Lovelace changed the status of Apollo',
    )
    expect(screen.queryByRole('link')).toBeNull()
  })

  it('leaves out the time of a change it cannot read', () => {
    // Act
    renderList([
      buildReadNotification({ occurredOn: 'not a date' as unknown as Date }),
    ])

    // Assert
    expect(screen.getByRole('link')).toHaveTextContent('Project APOLLO')
    expect(document.querySelector('time')).toBeNull()
  })
})
