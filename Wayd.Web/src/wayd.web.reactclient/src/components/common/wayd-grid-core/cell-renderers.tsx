import Link from 'next/link'
import type { ReactNode } from 'react'

import { teamUrl, type TeamUrlTarget } from '@/src/utils'

/**
 * Cell renderers for WaydGrid2 columns.
 *
 * Unlike the ag-grid `wayd-grid-cell-renderers.tsx` (whose renderers take
 * ag-grid's `CustomCellRendererProps`), a TanStack `cell` already receives
 * `row.original`, so these are plain functions of the domain object — call them
 * inline: `cell: ({ row }) => renderTeamLink(row.original.team)`. Each returns
 * `null` for a missing value so an empty cell renders nothing.
 *
 * This is the flat-list counterpart to the ag-grid renderers; as more grids
 * migrate to WaydGrid2 we add the matching link renderers here (planning
 * interval, project, portfolio, sprint, user, …), each reusing the shared URL
 * helpers in `@/src/utils`.
 */

/** A team/team-of-teams reference that can be rendered as a link. */
export interface TeamLinkTarget extends TeamUrlTarget {
  name: string
}

/**
 * Renders a team (or team-of-teams) as a link to its page, routing via the
 * shared {@link teamUrl} helper. Returns `null` when there's no team.
 */
export const renderTeamLink = (
  team: TeamLinkTarget | null | undefined,
): ReactNode => {
  if (!team) return null
  return <Link href={teamUrl(team)}>{team.name}</Link>
}

/**
 * A `NavigationDto`-shaped reference — a keyed entity with a display name.
 * Covers planning intervals, projects, portfolios, programs, etc.
 */
export interface NavLinkTarget {
  key: number | string
  name: string
}

/**
 * Renders a keyed entity as a `<Link>` to `hrefBase/{key}` with its name, or
 * `null` when absent. The base building block for the simple `NavigationDto`
 * link renderers below (whose routes are plain, non-branching templates, so —
 * unlike teams — they don't warrant a separate `*Url` helper).
 */
const renderNavLink = (
  entity: NavLinkTarget | null | undefined,
  hrefBase: string,
): ReactNode => {
  if (!entity) return null
  return <Link href={`${hrefBase}/${entity.key}`}>{entity.name}</Link>
}

/** Renders a planning interval as a link to its page. */
export const renderPlanningIntervalLink = (
  planningInterval: NavLinkTarget | null | undefined,
): ReactNode => renderNavLink(planningInterval, '/planning/planning-intervals')

/** Renders a project as a link to its page. */
export const renderProjectLink = (
  project: NavLinkTarget | null | undefined,
): ReactNode => renderNavLink(project, '/ppm/projects')

/** Renders a portfolio as a link to its page. */
export const renderPortfolioLink = (
  portfolio: NavLinkTarget | null | undefined,
): ReactNode => renderNavLink(portfolio, '/ppm/portfolios')

/** Renders a program as a link to its page. */
export const renderProgramLink = (
  program: NavLinkTarget | null | undefined,
): ReactNode => renderNavLink(program, '/ppm/programs')

/** Renders a workspace as a link to its page. */
export const renderWorkspaceLink = (
  workspace: NavLinkTarget | null | undefined,
): ReactNode => renderNavLink(workspace, '/work/workspaces')

/** A sprint reference, optionally carrying its team's code for the label. */
export interface SprintLinkTarget extends NavLinkTarget {
  team?: { code?: string | null } | null
}

/**
 * Renders a sprint as a link to its page. By default the team code is appended
 * as `Name (CODE)` when present (matching the ag-grid renderer); pass
 * `showTeamCode: false` to show just the name. Returns `null` when absent.
 */
export const renderSprintLink = (
  sprint: SprintLinkTarget | null | undefined,
  { showTeamCode = true }: { showTeamCode?: boolean } = {},
): ReactNode => {
  if (!sprint) return null
  const code = sprint.team?.code
  const label = showTeamCode && code ? `${sprint.name} (${code})` : sprint.name
  return <Link href={`/planning/sprints/${sprint.key}`}>{label}</Link>
}

/** A user reference: identified by `id`, labeled by `userName`. */
export interface UserLinkTarget {
  id: string
  userName?: string | null
}

/**
 * Renders a user as a link to their management page. Returns `null` when
 * absent. Users route by `id` (not `key`) and are labeled by `userName`.
 */
export const renderUserLink = (
  user: UserLinkTarget | null | undefined,
): ReactNode => {
  if (!user) return null
  return (
    <Link href={`/settings/user-management/users/${user.id}`}>
      {user.userName}
    </Link>
  )
}
