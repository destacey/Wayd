import { ProductStatusAlias } from '@/src/services/wayd-api'

export interface IncidentStatusStepSource {
  id: string
  name: string
  order: number
  isRetired: boolean
  /** The alias in the module's vocabulary, or null when the status has none. */
  aliasName?: string | null
}

export type IncidentStepStatus = 'finish' | 'process' | 'wait' | 'error'

export interface IncidentStatusStep {
  title: string
  status: IncidentStepStatus
  /**
   * The instant this status stores. Null when the status records none, or that
   * instant has not been recorded yet.
   */
  at: Date | string | null
}

export interface IncidentStatusInstants {
  noticedAt?: Date | string | null
  startedAt?: Date | string | null
  recoveredAt?: Date | string | null
}

/** How an incident moves forward. Cancel is not a step on this path. */
const PROGRESS = [
  ProductStatusAlias.Open,
  ProductStatusAlias.Responding,
  ProductStatusAlias.Resolved,
  ProductStatusAlias.Closed,
] as const

/**
 * The instant the alias stores. Closed and canceled record none, and a status
 * with no alias records none.
 */
function storedInstant(
  aliasName: string | null | undefined,
  instants: IncidentStatusInstants,
): Date | string | null {
  switch (aliasName) {
    case ProductStatusAlias.Open:
      return instants.noticedAt ?? null
    case ProductStatusAlias.Responding:
      return instants.startedAt ?? null
    case ProductStatusAlias.Resolved:
      return instants.recoveredAt ?? null
    default:
      return null
  }
}

/** A canceled incident has passed a status only when that status's instant is stored. */
function reachedBeforeCancel(
  aliasName: string | null | undefined,
  instants: IncidentStatusInstants,
): boolean {
  return storedInstant(aliasName, instants) != null
}

/**
 * The incident's workflow statuses in workflow order.
 *
 * Passed statuses are complete, the current one is in progress, and a canceled
 * incident marks that status canceled. Retired statuses stay out unless the
 * incident still holds one.
 */
export function incidentStatusSteps(
  statuses: readonly IncidentStatusStepSource[],
  currentStatusId: string,
  instants: IncidentStatusInstants = {},
): IncidentStatusStep[] {
  const visible = statuses
    .filter((status) => !status.isRetired || status.id === currentStatusId)
    .toSorted((left, right) => left.order - right.order)

  const current = visible.find((status) => status.id === currentStatusId)

  return visible.map((status) => ({
    title: status.name,
    status: stepVisual(status, current, currentStatusId, instants),
    at: storedInstant(status.aliasName, instants),
  }))
}

function stepVisual(
  status: IncidentStatusStepSource,
  current: IncidentStatusStepSource | undefined,
  currentStatusId: string,
  instants: IncidentStatusInstants,
): IncidentStepStatus {
  const canceled = current?.aliasName === ProductStatusAlias.Canceled
  if (status.id === currentStatusId) return canceled ? 'error' : 'process'
  if (canceled) {
    return reachedBeforeCancel(status.aliasName, instants) ? 'finish' : 'wait'
  }
  if (status.aliasName === ProductStatusAlias.Canceled) return 'wait'

  const currentRank = progressRank(current?.aliasName)
  const rank = progressRank(status.aliasName)
  if (currentRank >= 0 && rank >= 0) {
    return rank < currentRank ? 'finish' : 'wait'
  }
  return status.order < (current?.order ?? 0) ? 'finish' : 'wait'
}

function progressRank(aliasName: string | null | undefined): number {
  return PROGRESS.indexOf(aliasName as (typeof PROGRESS)[number])
}
