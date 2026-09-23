---
name: wayd-teams
description: Guides agents working with Wayd Teams via the Wayd MCP server. Use when looking up teams, resolving a team name to an ID, or assessing the health of a team's backlog.
---

# Wayd Teams

## When to use

- Listing all teams in the organization
- Looking up a specific team's details
- Resolving a team name to an integer ID for use in other tools (e.g. Planning Interval team filters)
- Finding out when a team was created, renamed, activated or deactivated
- Assessing a team's backlog: runway, net flow, WIP load, stale or aging work, readiness gaps, carry-over, rank inversions

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

### Backlog health

`Teams_GetBacklogHealth` takes `idOrCode` — the team's UUID or its **code**, not the integer `id` the other team tools use. Get either from `Teams_GetTeam`.

- **Read the outcome before the grade.** A check with outcome `Not Enough History` (fewer than 10 completed work items in the history window) or `Not Applicable` (nothing in scope) has no grade. It is not Healthy — say it could not be assessed.
- **`value` means different things.** Runway is weeks, Net Flow is work items created per completed, WIP Load is active work items per member; for every other check it is the percent of in-scope work items flagged, with `flagged` and `inScope` as the counts.
- **Name the work items.** To explain a problem, filter `workItems` to those whose `flags` include the check, and cite their keys and ranks rather than only the percentage.
- **Thresholds are what-ifs, not settings.** Pass any threshold to see how the grades change; nothing is saved. Quote the `thresholds` in the response when reporting grades, since they may not be the defaults.
- **Readiness checks cover only the top of the backlog.** Missing Story Points, Oversized, No Parent and No Project look at `readinessWindowWorkItems` top-ranked items, not the whole backlog.
- **Rank Inversion compares only dependencies within the team.** Cross-team dependencies are not ranked against each other.

### Common usage patterns

- **PI team filters** — many PI endpoints (e.g. `PlanningIntervals_GetObjectives`, `PlanningIntervals_GetRisks`) accept an optional `teamId` (UUID from the PI context, not the organization team integer ID). To get predictability for a single team, use `PlanningIntervals_GetTeamPredictability` with a `teamId`; `PlanningIntervals_GetPredictability` returns all teams and does not accept a `teamId`. Use `PlanningIntervals_GetTeams` to resolve PI team UUIDs — reserve `Teams_GetTeams` for organization-level team lookups.
- **Project team roles** — `sponsorIds`, `ownerIds`, `managerIds`, `memberIds` on projects take user UUIDs, not team IDs. Use `Users_GetUsers` to resolve those.
