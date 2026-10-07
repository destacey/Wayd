import { SprintScopeDto } from '@/src/services/wayd-api'
import { formatInstantInZone } from '@/src/utils'

/**
 * Where a sprint's commitment point and end came from, and the zone their
 * times are in.
 */
export const sprintScopeWindowText = (scope: SprintScopeDto): string[] => {
  const start = formatInstantInZone(scope.effectiveStart, scope.timeZone)
  const end = formatInstantInZone(scope.effectiveEnd, scope.timeZone)

  return [
    `Committed at ${start}, ${
      scope.startIsActual
        ? 'when the team started the sprint'
        : 'the end of the commitment grace period after the planned start'
    }.`,
    `Ends at ${end}, ${
      scope.endIsActual
        ? 'when the team completed the sprint'
        : 'the end of the last planned day, or the next sprint’s start if sooner'
    }.`,
    scope.hasTeam
      ? `Times are in the team’s zone, ${scope.timeZone}.`
      : `This sprint has no team, so it uses the system default zone (${scope.timeZone}) and grace period, and counts work items.`,
  ]
}
