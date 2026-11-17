import type { Instant } from '@/src/utils/instant'
import { IncidentWorkflowAction } from '@/src/services/api/wayd.schemas'

/**
 * Which empty instant a lifecycle action will fill.
 * One value fills every empty date that action writes.
 */
export type IncidentTransitionDate = 'startedAt' | 'recoveredAt' | 'both'

export function incidentTransitionDate(
  action: IncidentWorkflowAction,
  incident: { startedAt?: Instant | null; recoveredAt?: Instant | null },
): IncidentTransitionDate | null {
  const started = incident.startedAt != null
  const recovered = incident.recoveredAt != null

  if (action === IncidentWorkflowAction.BeginResponding) {
    return started ? null : 'startedAt'
  }

  if (action === IncidentWorkflowAction.MarkRecovered) {
    if (!started && !recovered) return 'both'
    if (!recovered) return 'recoveredAt'
    if (!started) return 'startedAt'
    return null
  }

  return null
}
