import { FollowedRecordDto } from '@/src/services/wayd-api'
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
    default:
      return null
  }
}
