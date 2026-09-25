---
name: wayd-teams
description: Guides agents working with Wayd Teams and Teams of Teams via the Wayd MCP server. Use when looking up teams or teams of teams, resolving a team name to an ID, assessing the health of a team's backlog, forecasting when work will be done, or generating allocation reports showing where completed work went by portfolio, program, project, theme, or work type.
---

# Wayd Teams

## When to use

- Listing all teams or teams of teams in the organization
- Looking up a specific team's details
- Resolving a team name to an integer ID for use in other tools (e.g. Planning Interval team filters)
- Finding out when a team was created, renamed, activated or deactivated
- Assessing a team's backlog: runway, net flow, WIP load, stale or aging work, readiness gaps, carry-over, rank inversions
- Forecasting when a work item, PI objective or project will be done, or how many backlog work items a team will finish by a date
- Reporting and analyzing team or team of teams allocation: where completed Requirement-tier work went over a date range by portfolio, program, project, strategic theme, or work type

---

## Entity context

### Team

- Represents an organizational delivery team in Wayd
- Team `id` is an **integer** (not a UUID) — this is different from most other Wayd entities
- Teams also have a **UUID** and a string **`code`** (used by `Teams_GetBacklogHealth`, `Teams_GetThroughputForecast`, and `Teams_GetTeamAllocation`)
- Teams can be active or inactive; `Teams_GetTeams` returns active teams by default
- Pass `includeInactive: true` to include inactive teams

### Team of Teams

- Represents an organizational hierarchy of teams in Wayd
- Has a **UUID** and a string **`code`** (used by `TeamsOfTeams_GetAllocation`)
- An allocation report on a team of teams rolls up the completed work of every team beneath it: child teams, their children, and so on down the hierarchy
- Each work item counts toward the parent team of teams its team had on the day the item became Done

### Allocation Report

- **What counts**: Requirement-tier work items (e.g. User Stories, Bugs) whose status category became **Done** within the date range (UTC whole days, max range 366 days). Portfolio-tier items (Epics/Features) and Removed items never count.
- **Dimensions** (`dimension`):
  - `Portfolio` (default): Grouped by each project's portfolio.
  - `Program`: Grouped by each project's program (includes "No program" gap group).
  - `Project`: Grouped by each project.
  - `StrategicTheme`: Grouped by each project's effective themes (includes "No theme" gap group).
  - `WorkType`: Grouped by work type (e.g. User Story, Bug).
- **Gap Groups**:
  - **No project**: Completed work where neither the item nor its parents link to a project. Present in every dimension except WorkType. A high share highlights untracked keep-the-lights-on or off-roadmap work.
  - **No program** / **No theme**: Work on a project that lacks a program or theme. Kept distinct from No project.
- **Measures** (`measure`):
  - `Count` (default): Counts each work item as one. Available on all teams.
  - `StoryPoints`: Measured by estimate for teams that size in points. `unestimated` controls whether unestimated items are excluded (`Exclude`) or filled from the team's average for that work type (`TeamAverage`).
  - `TeamEffort`: Available on **teams of teams only**. Measures each team's split in its own sizing (story points, or items for count-sized teams), then combines teams in proportion to completed items. This avoids adding story points across teams whose sizing scales differ.
- **Theme Counting** (`themeCounting`):
  - `SplitEvenly` (default): Divides work evenly between a project's multiple themes so shares add to 100%.
  - `CountFully`: Credits full work to each theme (shares sum to > 100%).

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

### Delivery forecasts

Monte Carlo forecasts from each team's recent daily throughput (10,000 trials, history ending yesterday UTC). They need the `delivery-forecasting` feature flag; a 404 on an item you know exists means the flag is off — say so rather than reporting the record missing.

| Question                                                     | Tool                                     | Identifies the work by                                                                   |
| ------------------------------------------------------------ | ---------------------------------------- | ---------------------------------------------------------------------------------------- |
| When will this work item be done?                            | `Workspaces_GetWorkItemForecast`         | `idOrKey` (workspace key — the prefix of the work item key) + `workItemKey` (`CORE-123`) |
| When will this PI objective's work be done?                  | `PlanningIntervals_GetObjectiveForecast` | `idOrKey` + `objectiveIdOrKey`                                                           |
| When will this project's work be done?                       | `Projects_GetForecast`                   | `idOrKey` (project UUID or key)                                                          |
| How many backlog work items will this team finish by a date? | `Teams_GetThroughputForecast`            | `idOrCode` (team UUID or code, not the integer `id`) + required `targetDate`             |

- **Read the `outcome` first.** Only `Forecast` fills `percentiles`. `Done`, `Not Enough History` (the team finished fewer than 10 backlog work items in the window), `Blocked by Dependency`, `Cannot Forecast` and `Nothing Remaining` each mean there are no dates — report which, and why from `issues`.
- **Report a range, lead with 85%.** Quote the 85% date (or count) as the planning figure and the 50% as a coin flip; never collapse the forecast to one date. A percentile with a null `date` did not finish within two years — say "beyond 2 years".
- **Chance by target date.** `chanceOfFinishingByTargetDate` is 0 to 1; state it as a percent against `targetDate`. An objective defaults to its target date, else the PI's end; a project to its planned end; a work item has none unless you pass `targetDate`.
- **Exclusions make it a lower bound.** When `excludedWorkItems` is non-empty, the dates cover only what could be forecast — say so and list the excluded keys with their issue.
- **Name the dependency to chase.** `dependencies[].shareOfTrialsSettingFinish` is how often waiting on that predecessor decided the finish; cite the highest. To show what dependencies cost, compare against `ignoreDependencies: true` and label that result a what-if. `ignoredDependencies` lists links left out (Predecessor Removed, Closes a Cycle) that likely need cleaning up.
- **Backlog position drives a work item's date.** Everything ahead of it in its team's backlog counts; report `backlogPosition` alongside the date. By default active items count ahead of proposed ones (`startedWorkFirst`, since teams usually finish what they have started), so an active item can sit ahead of its rank; pass `startedWorkFirst: false` for rank order alone, and say which order a forecast used.
- **Team throughput** answers with a count: each percentile's `workItems` is finished at least that often, and `throughWorkItem` names how far down the backlog that reaches ("at 85%, through CORE-123").
- **History window.** `lookbackDays` (14–365, default 90). Use a shorter window when a team's pace recently changed, and state the window you used. Nothing is saved.

### Allocation reports

An allocation report shows where completed Requirement-tier work went over a date range.

| Goal                                        | Tool                         | Identifies the team by                               | Required params                |
| ------------------------------------------- | ---------------------------- | ---------------------------------------------------- | ------------------------------ |
| Single team allocation                      | `Teams_GetTeamAllocation`    | `idOrCode` (team UUID or code, not the integer `id`) | `from`, `to` (YYYY-MM-DD, UTC) |
| List all teams of teams                     | `TeamsOfTeams_GetList`       |                                                      | Optional `includeInactive`     |
| Team of teams allocation (hierarchy rollup) | `TeamsOfTeams_GetAllocation` | `idOrCode` (team of teams UUID or code)              | `from`, `to` (YYYY-MM-DD, UTC) |

#### Parameters & Options:

- **`from` and `to`**: Required date strings (`YYYY-MM-DD`, UTC whole days). Range must not exceed 366 days.
- **`dimension`**: `"Portfolio"` (default), `"Program"`, `"Project"`, `"StrategicTheme"`, or `"WorkType"`.
- **`measure`**:
  - `"Count"` (default): Each completed item counts as 1.
  - `"StoryPoints"`: Story points for teams that size in points.
  - `"TeamEffort"`: **Teams of teams only.** Measures each team's split in its own sizing (story points or count), then combines teams in proportion to items completed. Strongly recommended for teams of teams to prevent mixing differing estimation scales.
- **`unestimated`**: When measuring story points, `"Exclude"` (default, leaves out unestimated items) or `"TeamAverage"` (fills in the team's average points for that work type).
- **`themeCounting`**: When grouping by `StrategicTheme`, `"SplitEvenly"` (default, divides work equally among multiple themes so total is 100%) or `"CountFully"` (credits whole work to each theme, total > 100%).

#### Reading and interpreting the report:

- **Start with `summary`**: Check `summary.totalItems` and `summary.totalStoryPoints`. For story points, check estimate coverage — low coverage weakens the reliability of point-based allocation.
- **Spot "No project"**: In every dimension except WorkType, look at the `NoProject` group (`kind: 1`). While some keep-the-lights-on work without a project is expected, a high share (e.g. > 20-30%) indicates significant delivery effort escaping portfolio visibility.
- **Identify top consumers**: Look at `groups` ordered by share. On a team of teams, check `largestContributor` to see which child team delivered the most work for that group.
- **Team-by-team breakdown**: On a team of teams, inspect `teams` to see individual child team contributions and spot teams with heavy "No project" or single-initiative dedication.

### Common usage patterns

- **PI team filters** — many PI endpoints (e.g. `PlanningIntervals_GetObjectives`, `PlanningIntervals_GetRisks`) accept an optional `teamId` (UUID from the PI context, not the organization team integer ID). To get predictability for a single team, use `PlanningIntervals_GetTeamPredictability` with a `teamId`; `PlanningIntervals_GetPredictability` returns all teams and does not accept a `teamId`. Use `PlanningIntervals_GetTeams` to resolve PI team UUIDs — reserve `Teams_GetTeams` for organization-level team lookups.
- **Project team roles** — `sponsorIds`, `ownerIds`, `managerIds`, `memberIds` on projects take user UUIDs, not team IDs. Use `Users_GetUsers` to resolve those.
