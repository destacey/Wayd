import { ProjectDeliverableDto } from '@/src/services/api/wayd.schemas'
import {
  compareCalendarDates,
  todayCalendarDate,
} from '@/src/utils/calendar-date'

/** Still open, and past its target date. */
export const isOverdue = (deliverable: ProjectDeliverableDto) =>
  deliverable.isOpen &&
  !!deliverable.targetDate &&
  compareCalendarDates(deliverable.targetDate, todayCalendarDate()) < 0
