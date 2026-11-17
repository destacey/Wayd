import { SprintType } from '@/src/services/api/wayd.schemas'

/**
 * Display names for the sprint types, the same as the server's lookup. Held
 * here so a sprint's type reads the same everywhere without waiting on it; the
 * set only changes with a release.
 */
export const sprintTypeLabels: Record<SprintType, string> = {
  [SprintType.Standard]: 'Standard',
  [SprintType.NonStandard]: 'Non-standard',
}
