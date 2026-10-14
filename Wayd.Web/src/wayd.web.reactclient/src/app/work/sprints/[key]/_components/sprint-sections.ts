import { RecordSection } from '@/src/components/common/record'

export enum SprintSections {
  Overview = 'overview',
  Backlog = 'backlog',
  Capacity = 'capacity',
  Activities = 'activities',
}

/**
 * The sprint page's sections. Capacity is offered only with the Team Capacity
 * Management feature on and to someone who may view teams, whose capacity it
 * shows; otherwise the page is as it was before capacity existed.
 */
export function sprintSections(showsCapacity: boolean): RecordSection[] {
  return [
    { id: SprintSections.Overview, label: 'Overview' },
    { id: SprintSections.Backlog, label: 'Backlog' },
    ...(showsCapacity
      ? [{ id: SprintSections.Capacity, label: 'Capacity' }]
      : []),
    { id: SprintSections.Activities, label: 'Activity' },
  ]
}
