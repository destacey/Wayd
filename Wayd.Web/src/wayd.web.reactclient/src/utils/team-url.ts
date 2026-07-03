/**
 * A team-like navigation object: has a route key and, for tellings teams apart
 * from teams-of-teams, an optional `type`. Only `type === 'Team'` is a plain
 * team; anything else (including a missing type) routes as a team-of-teams,
 * preserving the long-standing behavior across the app.
 */
export interface TeamUrlTarget {
  key: number | string
  type?: string
}

/**
 * Builds the app route for a team or team-of-teams. Extracted so the many
 * places that link to a team (grids, details pages, cards) share one source of
 * truth instead of re-deriving the Team vs Team-of-Teams branch inline.
 */
export const teamUrl = (team: TeamUrlTarget): string =>
  team.type === 'Team'
    ? `/organizations/teams/${team.key}`
    : `/organizations/team-of-teams/${team.key}`
