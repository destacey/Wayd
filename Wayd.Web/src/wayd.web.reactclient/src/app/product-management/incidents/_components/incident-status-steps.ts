export interface IncidentStatusStepSource {
  id: string
  name: string
  order: number
  isRetired: boolean
}

export interface IncidentStatusStep {
  title: string
  /** The current status is process. Every other status, including Canceled, waits. */
  status: 'process' | 'wait'
}

/**
 * The incident's workflow statuses in workflow order, with the current one marked.
 *
 * Retired statuses stay out unless the incident still holds one. Nothing here is a
 * date or a completion percent: a step is either current or not.
 */
export function incidentStatusSteps(
  statuses: readonly IncidentStatusStepSource[],
  currentStatusId: string,
): IncidentStatusStep[] {
  return statuses
    .filter((status) => !status.isRetired || status.id === currentStatusId)
    .toSorted((left, right) => left.order - right.order)
    .map((status) => ({
      title: status.name,
      status: status.id === currentStatusId ? 'process' : 'wait',
    }))
}
