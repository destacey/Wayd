import { FollowedRecordDto } from '@/src/services/wayd-api'
import { FollowOwnerTypes } from '@/src/store/features/common/follows-api'

/** Short names for the kinds of record, sized to fit a list row. */
const typeLabels: Record<string, string> = {
  [FollowOwnerTypes.Project]: 'Project',
  [FollowOwnerTypes.Program]: 'Program',
  [FollowOwnerTypes.PlanningIntervalObjective]: 'PI Objective',
}

/** What to call a kind of record: its short name, else the name the API gives it. */
export const typeLabel = (ownerType: string, displayName?: string) =>
  typeLabels[ownerType] ?? displayName ?? 'Record'

/** Identifies a followed or notified record across owner types. */
export const recordId = (record: Pick<FollowedRecordDto, 'ownerType' | 'id'>) =>
  `${record.ownerType}:${record.id}`
