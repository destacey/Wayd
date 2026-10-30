import {
  EventActorKind,
  NotificationEvent,
  NotificationReason,
} from '@/src/services/wayd-api'
import {
  notificationAction,
  notificationActorName,
  notificationReasonText,
  otherUpdatesText,
  unreadCountText,
} from './notification-wording'

describe('notificationAction', () => {
  it.each([
    [NotificationEvent.Created, 'created'],
    [NotificationEvent.StatusChanged, 'changed the status of'],
    [NotificationEvent.LifecycleChanged, 'changed the lifecycle of'],
    [NotificationEvent.HealthReported, 'reported health on'],
    [NotificationEvent.TimelineChanged, 'changed the dates of'],
    [NotificationEvent.RolesChanged, 'changed the people on'],
    [NotificationEvent.Reparented, 'moved'],
    [NotificationEvent.ScoreRecorded, 'scored'],
    [NotificationEvent.Withdrawn, 'withdrew'],
    [NotificationEvent.Retitled, 'renamed'],
    [NotificationEvent.DetailsChanged, 'changed the details of'],
    [NotificationEvent.AssigneeChanged, 'changed the assignee of'],
    [NotificationEvent.ParticipantsChanged, 'changed the participants of'],
    [NotificationEvent.ChannelChanged, 'changed the channel of'],
    [NotificationEvent.MessagePosted, 'posted on'],
    [NotificationEvent.InternalNotePosted, 'added an internal note to'],
  ])('words a %s change', (event, expected) => {
    // Act & Assert
    expect(notificationAction(event)).toBe(expected)
  })

  it('falls back to "updated" for an event the client has no words for', () => {
    // Act & Assert
    expect(notificationAction('Archived' as NotificationEvent)).toBe('updated')
  })
})

describe('otherUpdatesText', () => {
  it.each([
    [1, ''],
    [2, ' and 1 other update'],
    [5, ' and 4 other updates'],
  ])('words an entry of %i changes', (count, expected) => {
    // Act & Assert
    expect(otherUpdatesText(count)).toBe(expected)
  })
})

describe('notificationActorName', () => {
  it('names the employee who made the change', () => {
    // Act
    const name = notificationActorName({
      actor: { id: 'emp-1', key: 1, name: 'Ada Lovelace' },
      actorKind: EventActorKind.User,
    })

    // Assert
    expect(name).toBe('Ada Lovelace')
  })

  it('names a person with no linked employee "Someone"', () => {
    // Act
    const name = notificationActorName({
      actor: undefined,
      actorKind: EventActorKind.User,
    })

    // Assert
    expect(name).toBe('Someone')
  })

  it.each([EventActorKind.Automation, EventActorKind.System])(
    'names a %s change "Wayd"',
    (actorKind) => {
      // Act
      const name = notificationActorName({ actor: undefined, actorKind })

      // Assert
      expect(name).toBe('Wayd')
    },
  )
})

describe('notificationReasonText', () => {
  it.each([
    [[NotificationReason.Following], 'You follow this'],
    [[NotificationReason.Participant], 'You take part in this'],
    [
      [NotificationReason.Following, NotificationReason.Participant],
      'You follow and take part in this',
    ],
    [
      [
        NotificationReason.Following,
        NotificationReason.Participant,
        NotificationReason.Requester,
      ],
      'You follow, take part in and requested this',
    ],
    [
      [NotificationReason.Following, NotificationReason.Mentioned],
      'You follow this · You were mentioned',
    ],
    [[NotificationReason.Assigned], 'It is assigned to you'],
    [[NotificationReason.Role], 'You work on this'],
    [
      [NotificationReason.Following, NotificationReason.Role],
      'You follow and work on this',
    ],
    [[NotificationReason.Desk], 'It is waiting on your desk'],
    [
      [NotificationReason.Following, NotificationReason.Thread],
      'You follow this · You are in the thread',
    ],
  ])('words %j', (reasons, expected) => {
    // Act & Assert
    expect(notificationReasonText(reasons)).toBe(expected)
  })

  it('leaves out a reason the client has no words for', () => {
    // Act
    const text = notificationReasonText([
      'Watching' as NotificationReason,
      NotificationReason.Following,
    ])

    // Assert
    expect(text).toBe('You follow this')
  })
})

describe('unreadCountText', () => {
  it.each([
    [undefined, undefined],
    [{ count: 0, isCapped: false }, undefined],
    [{ count: 7, isCapped: false }, '7'],
    [{ count: 99, isCapped: false }, '99'],
    [{ count: 99, isCapped: true }, '99+'],
  ])('shows %j as %s', (unread, expected) => {
    // Act & Assert
    expect(unreadCountText(unread)).toBe(expected)
  })
})
