import {
  SprintScopeEntry,
  SprintScopeItemDto,
  SprintScopeOutcome,
} from '@/src/services/wayd-api'

export const sprintScopeEntryLabels: Record<SprintScopeEntry, string> = {
  [SprintScopeEntry.Committed]: 'Committed',
  [SprintScopeEntry.Added]: 'Added',
}

export const sprintScopeOutcomeLabels: Record<SprintScopeOutcome, string> = {
  [SprintScopeOutcome.Completed]: 'Completed',
  [SprintScopeOutcome.Removed]: 'Completed as Removed',
  [SprintScopeOutcome.CarriedOver]: 'Carried Over',
  [SprintScopeOutcome.Descoped]: 'Descoped',
  [SprintScopeOutcome.Remaining]: 'Remaining',
}

/**
 * The categories a sprint's scope is filtered by. An item falls in one entry
 * category and one outcome category, so it shows under both; Completed includes
 * the items completed as Removed.
 */
export enum SprintScopeCategory {
  All = 'all',
  Committed = 'committed',
  Added = 'added',
  Completed = 'completed',
  CarriedOver = 'carriedOver',
  Descoped = 'descoped',
  Remaining = 'remaining',
}

export const sprintScopeCategoryLabels: Record<SprintScopeCategory, string> = {
  [SprintScopeCategory.All]: 'All',
  [SprintScopeCategory.Committed]: 'Committed',
  [SprintScopeCategory.Added]: 'Added',
  [SprintScopeCategory.Completed]: 'Completed',
  [SprintScopeCategory.CarriedOver]: 'Carried Over',
  [SprintScopeCategory.Descoped]: 'Descoped',
  [SprintScopeCategory.Remaining]: 'Remaining',
}

export const isInSprintScopeCategory = (
  item: SprintScopeItemDto,
  category: SprintScopeCategory,
): boolean => {
  switch (category) {
    case SprintScopeCategory.Committed:
      return item.entry === SprintScopeEntry.Committed
    case SprintScopeCategory.Added:
      return item.entry === SprintScopeEntry.Added
    case SprintScopeCategory.Completed:
      return (
        item.outcome === SprintScopeOutcome.Completed ||
        item.outcome === SprintScopeOutcome.Removed
      )
    case SprintScopeCategory.CarriedOver:
      return item.outcome === SprintScopeOutcome.CarriedOver
    case SprintScopeCategory.Descoped:
      return item.outcome === SprintScopeOutcome.Descoped
    case SprintScopeCategory.Remaining:
      return item.outcome === SprintScopeOutcome.Remaining
    default:
      return true
  }
}
