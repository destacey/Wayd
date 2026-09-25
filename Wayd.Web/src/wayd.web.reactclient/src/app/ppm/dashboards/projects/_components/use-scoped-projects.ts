'use client'

import { ProjectListDto } from '@/src/services/wayd-api'
import { useGetPortfolioProjectsQuery } from '@/src/store/features/ppm/portfolios-api'
import { useGetProgramProjectsQuery } from '@/src/store/features/ppm/programs-api'
import {
  useGetProjectsPlanSummariesQuery,
  useGetProjectsQuery,
} from '@/src/store/features/ppm/projects-api'
import {
  ALL_ROLES,
  DashboardScope,
  isPersonScope,
  PlanSummaries,
  scopeIsComplete,
} from './dashboard-model'

export interface ScopedProjectsResult {
  projects: ProjectListDto[] | undefined
  planSummaries: PlanSummaries
  isLoading: boolean
  error: unknown
  refetch: () => void
  /** The employee whose roles the list shows; null outside a person scope. */
  subjectEmployeeId: string | null
}

/**
 * Loads the projects for a scope from whichever endpoint owns them, and the
 * plan summaries to match.
 *
 * A person scope filters the general list by role and counts only the tasks
 * that person can see. A record scope takes the record's own project list and
 * counts every task: a portfolio view is about the plan, not about anyone's
 * assignments. Every hook is called on every render, skipped when not the
 * active scope, so React sees a stable hook order as the scope changes.
 */
export const useScopedProjects = (
  scope: DashboardScope,
  selectedStatuses: number[],
  selectedRoles: number[],
  myEmployeeId: string | null,
): ScopedProjectsResult => {
  const status = selectedStatuses.length > 0 ? selectedStatuses : undefined
  const ready = scopeIsComplete(scope)
  const personScope = isPersonScope(scope)

  // Me sends no employee: the server resolves the principal, whose linkage may
  // be newer than the token this page decoded.
  const employeeId =
    scope.kind === 'person' ? (scope.employeeId ?? undefined) : undefined

  const people = useGetProjectsQuery(
    {
      status,
      role: selectedRoles.length > 0 ? selectedRoles : ALL_ROLES,
      employeeId,
    },
    { skip: !ready || !personScope },
  )
  const everything = useGetProjectsQuery(
    { status },
    { skip: scope.kind !== 'all' },
  )
  const portfolio = useGetPortfolioProjectsQuery(
    {
      portfolioIdOrKey:
        scope.kind === 'portfolio' ? (scope.portfolioId ?? '') : '',
      status,
    },
    { skip: !ready || scope.kind !== 'portfolio' },
  )
  const program = useGetProgramProjectsQuery(
    {
      programIdOrKey: scope.kind === 'program' ? (scope.programId ?? '') : '',
      status,
    },
    { skip: !ready || scope.kind !== 'program' },
  )

  const active = personScope
    ? people
    : scope.kind === 'all'
      ? everything
      : scope.kind === 'portfolio'
        ? portfolio
        : program

  const projectIds = active.data?.map((p) => p.id) ?? []
  const summaries = useGetProjectsPlanSummariesQuery(
    {
      projectIds,
      role: personScope && selectedRoles.length > 0 ? selectedRoles : undefined,
      employeeId,
      allTasks: !personScope,
    },
    { skip: projectIds.length === 0 },
  )

  return {
    projects: ready ? active.data : undefined,
    planSummaries: summaries.data ?? {},
    isLoading: ready && active.isLoading,
    error: active.error,
    refetch: active.refetch,
    subjectEmployeeId:
      scope.kind === 'me'
        ? myEmployeeId
        : scope.kind === 'person'
          ? scope.employeeId
          : null,
  }
}
