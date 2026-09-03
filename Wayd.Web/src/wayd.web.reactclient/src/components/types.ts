export interface OptionModel<T = string> {
  value: T
  label: string
}

/**
 * An option for a status, optionally shown in its own color rather than a neutral one.
 *
 * Carries the lifecycle category rather than a color: the category is what the API returns
 * and what every other status surface keys its color off, so a filter button and the tag or
 * timeline bar for the same status cannot drift apart.
 *
 * Optional because the same filter bar also lists *states* — roadmaps, strategic themes —
 * which have no lifecycle category. Those stay a neutral color rather than borrowing a
 * meaning they do not have.
 */
export interface StatusOptionModel extends OptionModel<number> {
  lifecycleCategory?: string
}

export interface DateRange {
  start?: Date
  end?: Date
}

// Iteration States from Wayd.Common.Domain.Enums.Work.IterationState
export enum IterationState {
  Unknown = 0,
  Completed = 1,
  Active = 2,
  Future = 3,
}

// Work Type Tiers from Wayd.Common.Domain.Enums.Work.WorkTypeTier
export enum WorkTypeTier {
  Portfolio = 0,
  Requirement = 1,
  Task = 2,
  Other = 3,
}

// Work Status Categories from Wayd.Common.Domain.Enums.Work.WorkStatusCategory
export enum WorkStatusCategory {
  Proposed = 0,
  Active = 1,
  Done = 2,
  Removed = 3,
}

export enum DependencyHealth {
  Healthy = 1,
  AtRisk = 2,
  Unhealthy = 3,
  Unknown = 4,
}

export interface SprintMetricsData {
  completed: number
  inProgress: number
  notStarted: number
  completedStoryPoints: number
  inProgressStoryPoints: number
  notStartedStoryPoints: number
  missingStoryPoints: number
}

export enum ProjectStatus {
  Proposed = 1,
  Active = 2,
  Completed = 3,
  Canceled = 4,
}

// Unordered categories: the numeric values are stable identifiers.
// Do not infer lifecycle direction by comparing them.
export enum LifecycleCategory {
  NotStarted = 0,
  Active = 1,
  Completed = 2,
  Canceled = 3,
}
