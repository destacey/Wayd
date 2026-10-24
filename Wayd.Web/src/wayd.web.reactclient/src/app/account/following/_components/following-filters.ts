import {
  FollowCountsDto,
  FollowedRecordDto,
  FollowView,
  HealthStatus,
} from '@/src/services/wayd-api'

/** What narrows the list: one kind of record, one "needs a look" view, both or neither. */
export interface FollowingFilter {
  ownerType?: string
  view?: FollowView
}

/**
 * The "needs a look" views, in the order the page lists them. Each is a named
 * query the API counts and filters on; a new one is a new entry here.
 */
export const followViews: { view: FollowView; label: string }[] = [
  { view: FollowView.Unhealthy, label: 'Health is red' },
  { view: FollowView.ChangedRecently, label: 'Changed this week' },
  { view: FollowView.Closed, label: 'Closed or done' },
]

/** Below this many followed records, the rail and filter row are noise. */
export const LowCountThreshold = 10

/** The count the API returned for a view, or 0. */
export const viewCount = (
  counts: FollowCountsDto | undefined,
  view: FollowView,
) => counts?.views.find((v) => v.view === view)?.count ?? 0

/** Identifies a followed record across owner types. */
export const recordId = (record: Pick<FollowedRecordDto, 'ownerType' | 'id'>) =>
  `${record.ownerType}:${record.id}`

/** The antd Badge status for a record's health. */
export const healthBadgeStatus = (
  health: HealthStatus | undefined,
): 'success' | 'warning' | 'error' | 'default' => {
  switch (health) {
    case HealthStatus.Healthy:
      return 'success'
    case HealthStatus.AtRisk:
      return 'warning'
    case HealthStatus.Unhealthy:
      return 'error'
    default:
      return 'default'
  }
}

/** What to call a record's health in text. */
export const healthLabel = (health: HealthStatus | undefined): string => {
  switch (health) {
    case HealthStatus.Healthy:
      return 'Healthy'
    case HealthStatus.AtRisk:
      return 'At risk'
    case HealthStatus.Unhealthy:
      return 'Unhealthy'
    default:
      return 'No health check'
  }
}
