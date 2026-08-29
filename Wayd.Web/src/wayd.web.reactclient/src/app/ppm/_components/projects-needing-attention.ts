import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { ProjectListDto } from '@/src/services/wayd-api'

export type AttentionSortMode = 'health' | 'name' | 'rank' | 'program'

/** The health statuses that mean somebody should look at this project. */
const ATTENTION_STATUSES = new Set(['At Risk', 'Unhealthy'])

/** Worst first, so the most urgent project heads the list. */
const HEALTH_PRIORITY: Record<string, number> = {
  Unhealthy: 0,
  'At Risk': 1,
}

const isClosed = (project: ProjectListDto) =>
  project.status.lifecycleCategory === 'Completed' ||
  project.status.lifecycleCategory === 'Canceled'

const healthRank = (project: ProjectListDto) =>
  HEALTH_PRIORITY[project.healthCheck?.status.name ?? ''] ?? 99

/**
 * Projects flagged At Risk or Unhealthy, worst first.
 *
 * Closed projects are excluded for the same reason they are left out of the
 * health chart: a completed or cancelled project's last health check describes
 * work that is over, and nobody can act on it.
 *
 * Health is always the tiebreaker, whichever mode is chosen — within a program
 * or among names that sort together, the unhealthy one should still come
 * first. Then name, then key: keys are unique, so the order is total and
 * stable rather than dependent on the order the API happened to return.
 */
export const getProjectsNeedingAttention = (
  projects: ProjectListDto[],
  sortMode: AttentionSortMode,
): ProjectListDto[] => {
  const flagged = projects.filter(
    (project) =>
      !isClosed(project) &&
      ATTENTION_STATUSES.has(project.healthCheck?.status.name ?? ''),
  )

  return flagged.sort((a, b) => {
    if (sortMode === 'name') {
      const byName = caseInsensitiveCompare(a.name, b.name)
      if (byName !== 0) return byName
    }

    if (sortMode === 'rank') {
      // Matches the card view's own rank comparer: an unranked project sorts
      // last rather than to the top, which is where a missing position would
      // otherwise put it.
      const aPosition = a.position ?? Number.MAX_SAFE_INTEGER
      const bPosition = b.position ?? Number.MAX_SAFE_INTEGER
      const byRank = aPosition - bPosition
      if (byRank !== 0) return byRank
    }

    if (sortMode === 'program') {
      // Projects held directly by the portfolio have no program. They sort
      // last rather than under an empty heading.
      const aProgram = a.program?.name ?? ''
      const bProgram = b.program?.name ?? ''
      if (aProgram !== bProgram) {
        if (!aProgram) return 1
        if (!bProgram) return -1
        return caseInsensitiveCompare(aProgram, bProgram)
      }
    }

    const byHealth = healthRank(a) - healthRank(b)
    if (byHealth !== 0) return byHealth

    const byName = caseInsensitiveCompare(a.name, b.name)
    if (byName !== 0) return byName

    // Keys are unique, so this is what makes the order total. Compared with
    // caseInsensitiveCompare for its numeric handling: PRJ-2 before PRJ-10.
    return caseInsensitiveCompare(a.key, b.key)
  })
}
