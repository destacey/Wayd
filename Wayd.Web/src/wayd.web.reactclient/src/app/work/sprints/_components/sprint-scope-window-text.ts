import { SprintScopeDto } from '@/src/services/wayd-api'
import { calendarDateInZone, formatInstantInZone } from '@/src/utils'

/**
 * The calendar day an instant closes, when it falls at midnight in `timeZone`:
 * a default commitment point or end is the end of a day, and "12:00 AM Oct 12"
 * reads as the wrong day where "the end of Oct 11" does not.
 */
const dayEndedBy = (instant: Date, timeZone: string): string | null => {
  const justBefore = new Date(new Date(instant).getTime() - 1)
  if (
    calendarDateInZone(justBefore, timeZone) ===
    calendarDateInZone(instant, timeZone)
  )
    return null

  return new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(justBefore)
}

/**
 * Where a sprint's commitment point and end came from. A moment the team
 * recorded is shown on the viewer's clock, as the sprint's details show it; a
 * default one is the end of a day in the team's zone, so it is shown as that day.
 */
export const sprintScopeWindowText = (
  scope: SprintScopeDto,
  viewerZone: string = Intl.DateTimeFormat().resolvedOptions().timeZone,
  now: Date = new Date(),
): string[] => {
  const when = (instant: Date) => {
    const day = dayEndedBy(instant, scope.timeZone)
    return day ? `the end of ${day}` : formatInstantInZone(instant, viewerZone)
  }
  const ended = now.getTime() >= new Date(scope.effectiveEnd).getTime()

  return [
    `Committed at ${when(scope.effectiveStart)}, ${
      scope.startIsActual
        ? 'when the team started the sprint'
        : 'when the commitment grace period after the planned start ended'
    }.`,
    `${ended ? 'Ended' : 'Ends'} at ${when(scope.effectiveEnd)}, ${
      scope.endIsActual
        ? 'when the team completed the sprint'
        : 'its last planned day, or when the next sprint starts if sooner'
    }.`,
    scope.hasTeam
      ? `Days are counted in the team’s zone, ${scope.timeZone}.`
      : `This sprint has no team, so it uses the system default zone (${scope.timeZone}) and grace period, and counts work items.`,
  ]
}
