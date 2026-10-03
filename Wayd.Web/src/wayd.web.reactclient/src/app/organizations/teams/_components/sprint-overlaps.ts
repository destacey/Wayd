import { compareCalendarDates } from '@/src/utils'

interface DatedSprint {
  name: string
  start: string
  end: string
}

/**
 * The sprints whose planned dates overlap the next sprint's, in start order.
 *
 * Both ends are inclusive calendar days, so a sprint ending on the day the next
 * one starts overlaps it.
 */
export const findOverlappingSprints = <T extends DatedSprint>(
  sprints: readonly T[],
): T[] => {
  // A sprint without planned dates has nothing to overlap, and sorting it as an
  // empty string would pair it with its neighbours.
  const ordered = sprints
    .filter((sprint) => sprint.start && sprint.end)
    .sort((a, b) => compareCalendarDates(a.start, b.start))

  const overlapping = new Set<T>()
  for (let i = 1; i < ordered.length; i++) {
    const previous = ordered[i - 1]
    const next = ordered[i]
    if (compareCalendarDates(previous.end, next.start) >= 0) {
      overlapping.add(previous)
      overlapping.add(next)
    }
  }

  return ordered.filter((sprint) => overlapping.has(sprint))
}

/** Names the first few sprints, then counts the rest. */
export const summarizeSprintNames = (
  sprints: readonly DatedSprint[],
  limit = 3,
): string => {
  const names = sprints.slice(0, limit).map((sprint) => sprint.name)
  const remaining = sprints.length - names.length
  return remaining > 0
    ? `${names.join(', ')} and ${remaining} more`
    : names.join(', ')
}
