import { ProjectDeliverableDto } from '@/src/services/wayd-api'
import {
  compareCalendarDates,
  todayCalendarDate,
} from '@/src/utils/calendar-date'

/** Still open, and past its target date. */
export const isOverdue = (deliverable: ProjectDeliverableDto) =>
  deliverable.isOpen &&
  !!deliverable.targetDate &&
  compareCalendarDates(deliverable.targetDate, todayCalendarDate()) < 0
