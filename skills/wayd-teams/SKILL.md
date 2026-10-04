---
name: wayd-teams
description: Guides agents working with Wayd teams and teams of teams via the Wayd MCP server. Use when looking up teams or teams of teams, resolving a team name to its id, key, or code, reviewing a team's change history, assessing the health of a team's backlog, forecasting when work will be done, or generating allocation reports showing where completed work went by portfolio, program, project, strategic theme, or work type.
---

# Wayd Teams

## Team identifiers

A team carries three identifiers, and each tool accepts a specific one:

| Identifier | Example      | Accepted by                                                                                               |
| ---------- | ------------ | --------------------------------------------------------------------------------------------------------- |
| `id`       | UUID         | `Teams_GetActivities`, `Teams_GetBacklogHealth`, `Teams_GetThroughputForecast`, `Teams_GetTeamAllocation` |
| `key`      | integer      | `Teams_GetTeam` (its parameter is named `id` but takes the integer `key`), `Teams_GetActivities`          |
| `code`     | short string | `Teams_GetBacklogHealth`, `Teams_GetThroughputForecast`, `Teams_GetTeamAllocation`                        |

Resolve a team name with `Teams_GetTeams`, which returns all three (active teams only unless `includeInactive: true`). A team of teams has a UUID `id` and a `code`; resolve it with `TeamsOfTeams_GetList`.

Planning Interval tools take a different, PI-scoped team id — see the `wayd-pi` skill.

## Activity history

`Teams_GetActivities` returns the team's recorded changes, newest first: creation, detail changes, activation and deactivation, membership changes, team-of-teams membership, and operating models.

- Member entries carry employee and role ids, not names. Resolve employee ids against `employee.id` from `Users_GetUsers` (see the `wayd-users` skill); no tool resolves role ids. Report any id you cannot resolve as an id — never guess who or what it is.
- Each `payload` is a JSON string carrying the value before and after the change.
- A `Baseline` entry marks where tracking began for a team that already existed. Nothing before it is in the log, so do not report it as the team's creation.

## Backlog health

`Teams_GetBacklogHealth` takes the team's UUID `id` or `code`.

- **Read the outcome before the grade.** A check with outcome `Not Enough History` (fewer than 10 completed work items in the history window) or `Not Applicable` (nothing in scope) has no grade. It is not Healthy — say it could not be assessed.
- **`value` means different things.** Runway is weeks, Net Flow is work items created per completed, WIP Load is active work items per member; for every other check it is the percent of in-scope work items flagged, with `flagged` and `inScope` as the counts.
- **Name the work items.** To explain a problem, filter `workItems` to those whose `flags` include the check, and cite their keys and ranks rather than only the percentage.
- **Thresholds are what-ifs, not settings.** Pass any threshold to see how the grades change; nothing is saved. Quote the `thresholds` in the response when reporting grades, since they may not be the defaults.
- **Readiness checks cover only the top of the backlog.** Missing Estimate, Oversized, No Parent and No Project look at `readinessWindowWorkItems` top-ranked items, not the whole backlog.
- **Estimates are in the team's `sizingMethod`.** `totalEstimate`, `oversizedEstimate` and each work item's `estimate` are Story Points, Effort or Size as the response's `sizingMethod` says — name the unit. A work item with no value in that estimate is missing one even if it has another; 0 is an estimate. For a team that sizes by `Count`, Missing Estimate and Oversized are `Not Applicable`.
- **Rank Inversion compares only dependencies within the team.** Cross-team dependencies are not ranked against each other.

## Delivery forecasts

Monte Carlo forecasts from each team's recent daily throughput (10,000 trials, history ending yesterday UTC). They need the `delivery-forecasting` feature flag; a 404 on an item you know exists means the flag is off — say so rather than reporting the record missing.

| Question                                                     | Tool                                     | Identifies the work by                                                                   |
| ------------------------------------------------------------ | ---------------------------------------- | ---------------------------------------------------------------------------------------- |
| When will this work item be done?                            | `Workspaces_GetWorkItemForecast`         | `idOrKey` (workspace key — the prefix of the work item key) + `workItemKey` (`CORE-123`) |
| When will this PI objective's work be done?                  | `PlanningIntervals_GetObjectiveForecast` | `idOrKey` + `objectiveIdOrKey`                                                           |
| When will this project's work be done?                       | `Projects_GetForecast`                   | `idOrKey` (project UUID or key)                                                          |
| How many backlog work items will this team finish by a date? | `Teams_GetThroughputForecast`            | `idOrCode` (team UUID or code) + required `targetDate`                                   |

- **Read the `outcome` first.** Only `Forecast` fills `percentiles`. `Done`, `Not Enough History` (the team finished fewer than 10 backlog work items in the window), `Blocked by Dependency`, `Cannot Forecast` and `Nothing Remaining` each mean there are no dates — report which, and why from `issues`.
- **Report a range, lead with 85%.** Quote the 85% date (or count) as the planning figure and the 50% as a coin flip; never collapse the forecast to one date. A percentile with a null `date` did not finish within two years — say "beyond 2 years".
- **Chance by target date.** `chanceOfFinishingByTargetDate` is 0 to 1; state it as a percent against `targetDate`. An objective defaults to its target date, else the PI's end; a project to its planned end; a work item has none unless you pass `targetDate`.
- **Exclusions make it a lower bound.** When `excludedWorkItems` is non-empty, the dates cover only what could be forecast — say so and list the excluded keys with their issue.
- **Name the dependency to chase.** `dependencies[].shareOfTrialsSettingFinish` is how often waiting on that predecessor decided the finish; cite the highest. To show what dependencies cost, compare against `ignoreDependencies: true` and label that result a what-if. `ignoredDependencies` lists links left out (Predecessor Removed, Closes a Cycle) that likely need cleaning up.
- **Backlog position drives a work item's date.** Everything ahead of it in its team's backlog counts; report `backlogPosition` alongside the date. By default active items count ahead of proposed ones (`startedWorkFirst`, since teams usually finish what they have started), so an active item can sit ahead of its rank; pass `startedWorkFirst: false` for rank order alone, and say which order a forecast used.
- **Team throughput** answers with a count: each percentile's `workItems` is finished at least that often, and `throughWorkItem` names how far down the backlog that reaches ("at 85%, through CORE-123").
- **History window.** `lookbackDays` (14–365, default 90). Use a shorter window when a team's pace recently changed, and state the window you used. Nothing is saved.

## Allocation reports

An allocation report shows where a team's completed work went over a date range.

| Scope                                     | Tool                         | Identifies it by                        |
| ----------------------------------------- | ---------------------------- | --------------------------------------- |
| One team                                  | `Teams_GetTeamAllocation`    | `idOrCode` (team UUID or code)          |
| A team of teams and every team beneath it | `TeamsOfTeams_GetAllocation` | `idOrCode` (team of teams UUID or code) |

### What counts

Requirement-tier work items (e.g. User Stories, Bugs) whose status category became **Done** between `from` and `to` (`YYYY-MM-DD`, inclusive, UTC, at most 366 days apart). Portfolio-tier items (Epics, Features) and Removed items never count. On a team of teams, each work item counts toward the parent its team had on the day the item became Done.

### Parameters

- **`dimension`** — `Portfolio` (default), `Program`, `Project`, `StrategicTheme`, or `WorkType`. Use the default for both a team and a team of teams unless the user asks for another grouping.
- **`measure`** — `Count` (default; each item counts as 1) or `StoryPoints` (point-sized teams only). A team of teams also offers `TeamEffort`, which measures each team's split in its own sizing and combines teams by share of completed items. **For a team of teams, default to `TeamEffort`**: adding story points across teams mixes estimation scales that do not compare.
- **`unestimated`** — with `StoryPoints`: `Exclude` (default) leaves unestimated items out; `TeamAverage` fills them from the team's average for that work type.
- **`themeCounting`** — with `StrategicTheme`: `SplitEvenly` (default) divides a multi-theme project's work so shares total 100%; `CountFully` credits all of it to each theme, so shares exceed 100%.

### Reading the report

- **Start with `summary`.** `itemsCompleted` is the total. Under `StoryPoints`, estimate coverage is `estimatedItems` out of `itemsInPointSizedTeams` — low coverage weakens a point-based report — and `excludedTeams` lists teams the measure left out (such as count-sized teams); name them.
- **Gap groups are findings, not noise.** The `NoProject` group (`kind: "NoProject"`; every dimension except WorkType) is completed work where neither the item nor its parents link to a project; `summary.noProjectShare` is its percent of the total. A high share (above roughly 20–30%) means significant effort escaping portfolio visibility. `MissingLevel` groups (`No program`, `No theme`) are work on a project that lacks a program or theme, kept separate from `NoProject`.
- **Top consumers.** Read `groups` by share. On a team of teams, `largestContributor` names the child team that delivered the most for each group.
- **Team breakdown.** On a team of teams, `teams` shows each child team's contribution — look for teams heavy in `No project` or dedicated to a single initiative.
