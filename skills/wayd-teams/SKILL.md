---
name: wayd-teams
description: Guides agents working with Wayd Teams via the Wayd MCP server. Use when looking up teams or resolving a team name to an ID.
---

# Wayd Teams

## When to use

- Listing all teams in the organization
- Looking up a specific team's details
- Resolving a team name to an integer ID for use in other tools (e.g. Planning Interval team filters)
- Finding out when a team was created, renamed, activated or deactivated

---

## Entity context

### Team

- Represents an organizational team in Wayd
- Team `id` is an **integer** (not a UUID) — this is different from most other Wayd entities
- Teams can be active or inactive; `Teams_GetTeams` returns active teams by default
- Pass `includeInactive: true` to include inactive teams

---

## Instructions

### Listing teams

- All active teams: `Teams_GetTeams`
- Include inactive teams: `Teams_GetTeams` with `includeInactive: true`

### Getting a specific team

`Teams_GetTeam` requires `id` (integer). If you only have a name, call `Teams_GetTeams` first to resolve name → ID.

### Activity history

`Teams_GetActivities` with `idOrKey` (the team's integer ID or its UUID) returns the team's recorded changes, newest first: creation, detail changes, activation and deactivation. Membership changes are not part of it. Each entry's `payload` is a JSON string carrying the value before and after, and a `Baseline` entry marks where tracking began for a team that already existed.

### Common usage patterns

- **PI team filters** — many PI endpoints (e.g. `PlanningIntervals_GetObjectives`, `PlanningIntervals_GetRisks`) accept an optional `teamId` (UUID from the PI context, not the organization team integer ID). To get predictability for a single team, use `PlanningIntervals_GetTeamPredictability` with a `teamId`; `PlanningIntervals_GetPredictability` returns all teams and does not accept a `teamId`. Use `PlanningIntervals_GetTeams` to resolve PI team UUIDs — reserve `Teams_GetTeams` for organization-level team lookups.
- **Project team roles** — `sponsorIds`, `ownerIds`, `managerIds`, `memberIds` on projects take user UUIDs, not team IDs. Use `Users_GetUsers` to resolve those.
