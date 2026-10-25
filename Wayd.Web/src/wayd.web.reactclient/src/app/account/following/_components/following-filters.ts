import { FollowCountsDto, FollowView } from '@/src/services/wayd-api'

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
