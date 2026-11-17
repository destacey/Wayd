import { IntakeRequestOutcomeKind } from '@/src/services/api/wayd.schemas'

/** How each kind of intake request outcome is named wherever outcomes are shown. */
export const outcomeKindLabel: Record<IntakeRequestOutcomeKind, string> = {
  [IntakeRequestOutcomeKind.NewProject]: 'New project',
  [IntakeRequestOutcomeKind.ExistingProjectScope]: 'Existing project scope',
  [IntakeRequestOutcomeKind.WorkItem]: 'Work item',
}
