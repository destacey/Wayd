import { RecordSection } from '@/src/components/common/record'

export enum PlanningIntervalSections {
  Overview = 'overview',
  Objectives = 'objectives',
  PlanReview = 'plan-review',
  Risks = 'risks',
  Backlog = 'backlog',
  Capacity = 'capacity',
  HealthReport = 'health-report',
  Activities = 'activities',
}

/**
 * The planning interval page's sections. Capacity is offered only with the
 * Team Capacity Management feature on; otherwise the page is as it was before
 * capacity existed.
 */
export function planningIntervalSections(
  showsCapacity: boolean,
): RecordSection[] {
  return [
    { id: PlanningIntervalSections.Overview, label: 'Overview' },
    { id: PlanningIntervalSections.PlanReview, label: 'Plan Review' },
    { id: PlanningIntervalSections.Objectives, label: 'Objectives' },
    { id: PlanningIntervalSections.Risks, label: 'Risks' },
    { id: PlanningIntervalSections.Backlog, label: 'Backlog' },
    ...(showsCapacity
      ? [{ id: PlanningIntervalSections.Capacity, label: 'Capacity' }]
      : []),
    { id: PlanningIntervalSections.Activities, label: 'Activity' },
  ]
}
