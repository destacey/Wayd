import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import type { BreakdownDatum } from '@/src/app/ppm/_components/breakdown-pie'
import { DEPENDENCY_HEALTH_VALUES } from '@/src/components/common/work/dependency-health-colors'
import { ScopedDependencyDto, WorkItemListDto } from '@/src/services/wayd-api'

/** Cross-team is the scope worth reporting: intra-team needs no coordination. */
export const CROSS_TEAM_SCOPE = 'Cross-Team'

export const DEPENDENCY_HEALTH_TOOLTIP =
  'Health of this work item’s cross-team dependencies. Intra-team dependencies are excluded — they need no coordination between teams.'

/**
 * Direct children grouped by work type.
 *
 * Counts come from the same query the Child Work Items section renders, so a
 * slice can never disagree with the list it summarises. Only direct children
 * are counted — the grandchildren below them belong to their own parents.
 */
export const getChildTypeBreakdown = (
  children: WorkItemListDto[],
): BreakdownDatum[] => {
  const counts = new Map<string, number>()

  for (const child of children) {
    const name = child.type.name
    counts.set(name, (counts.get(name) ?? 0) + 1)
  }

  return [...counts.entries()]
    .sort(([a], [b]) => caseInsensitiveCompare(a, b))
    .map(([type, count]) => ({ type, count }))
}

/** The dependencies that cross a team boundary, which are the ones at risk. */
export const getCrossTeamDependencies = (
  dependencies: ScopedDependencyDto[],
): ScopedDependencyDto[] =>
  dependencies.filter((d) => d.scope?.name === CROSS_TEAM_SCOPE)

/**
 * Cross-team dependencies grouped by health.
 *
 * Carries no colors of its own — the chart takes
 * `getDependencyHealthColorScale`, which covers every health rather than only
 * the ones present. Ordered by severity to match that scale's domain, so the
 * slices read Healthy → At Risk → Unhealthy rather than alphabetically.
 */
export const getDependencyHealthBreakdown = (
  dependencies: ScopedDependencyDto[],
): BreakdownDatum[] => {
  const counts = new Map<string, number>()

  for (const dependency of getCrossTeamDependencies(dependencies)) {
    const name = dependency.health?.name ?? 'Unknown'
    counts.set(name, (counts.get(name) ?? 0) + 1)
  }

  const order = DEPENDENCY_HEALTH_VALUES.map((v) => v.name)

  return [...counts.entries()]
    .sort(([a], [b]) => order.indexOf(a) - order.indexOf(b))
    .map(([type, count]) => ({ type, count }))
}
