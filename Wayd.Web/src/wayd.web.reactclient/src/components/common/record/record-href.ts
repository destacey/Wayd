import { FollowedRecordDto } from '@/src/services/api/wayd.schemas'
import { FollowOwnerTypes } from '@/src/store/features/common/follows-api'

/**
 * The page a followed or notified record lives on, or null for an owner type
 * the client has no page for yet. A new followable type adds its route here.
 */
export const recordHref = (
  record: Pick<FollowedRecordDto, 'ownerType' | 'key' | 'parentKey'>,
): string | null => {
  switch (record.ownerType) {
    case FollowOwnerTypes.Project:
      return `/ppm/projects/${record.key}`
    case FollowOwnerTypes.Program:
      return `/ppm/programs/${record.key}`
    case FollowOwnerTypes.PlanningIntervalObjective:
      return record.parentKey
        ? `/planning/planning-intervals/${record.parentKey}/objectives/${record.key}`
        : null
    case FollowOwnerTypes.IntakeRequest:
      return `/demand-management/requests/${record.key}`
    case FollowOwnerTypes.Incident:
      return `/product-management/incidents/${record.key}`
    default:
      return null
  }
}

/** The owner types whose page has a Discussion section, each named `discussion`. */
const discussionOwnerTypes: string[] = [
  FollowOwnerTypes.Project,
  FollowOwnerTypes.IntakeRequest,
  FollowOwnerTypes.Incident,
]

/**
 * A record's Discussion section, opened on one message when given; the
 * record's page alone for a kind of record with no conversation, and null
 * where `recordHref` has no page.
 */
export const recordDiscussionHref = (
  record: Pick<FollowedRecordDto, 'ownerType' | 'key' | 'parentKey'>,
  messageId?: string,
): string | null => {
  const href = recordHref(record)
  if (!href || !discussionOwnerTypes.includes(record.ownerType)) return href
  const query = new URLSearchParams({ section: 'discussion' })
  if (messageId) query.set('message', messageId)
  return `${href}?${query.toString()}`
}
