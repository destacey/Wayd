import type {
  UpdateRoadmapActivityDatesRequest,
  UpdateRoadmapMilestoneDatesRequest,
  UpdateRoadmapTimeboxDatesRequest,
} from '@/src/services/api/wayd.schemas'
import type { CalendarDate } from '@/src/utils/calendar-date'

export type RoadmapItemDatesRequest =
  | UpdateRoadmapActivityDatesRequest
  | UpdateRoadmapMilestoneDatesRequest
  | UpdateRoadmapTimeboxDatesRequest

/**
 * The request that moves a roadmap item to `start`–`end`, as a drag on the
 * timeline or the grid's Gantt leaves it. A milestone is a single day, so it
 * carries `date` (the start) rather than a range; the server refuses a
 * milestone without one.
 */
export const itemDatesRequest = ({
  $type,
  roadmapId,
  itemId,
  start,
  end,
}: {
  /** The item's discriminator: `activity`, `milestone` or `timebox`. */
  $type: string
  roadmapId: string
  itemId: string
  start: CalendarDate
  end: CalendarDate
}): RoadmapItemDatesRequest =>
  $type === 'milestone'
    ? { $type: 'milestone', roadmapId, itemId, date: start }
    : {
        $type: $type === 'timebox' ? 'timebox' : 'activity',
        roadmapId,
        itemId,
        start,
        end,
      }
