---
name: wayd-teams
description: Read before calling the Wayd MCP tools named Teams_*, TeamsOfTeams_*, HolidayCalendars_*, TeamMemberRoles_*, TeamTypes_* or Risks_*. Guides agents working with Wayd teams, teams of teams and the organization settings behind them via the Wayd MCP server. Use when looking up teams or teams of teams, resolving a team name to its id, key, or code, creating, updating or deactivating a team or team of teams, arranging the team hierarchy, adding or removing members and their roles, setting or correcting a team's operating model (Scrum or Kanban, sizing, time zone, working week, holiday calendar, cadence), reading a team's capacity, backlog, work items, dependencies or sprints, reviewing a team's change history, assessing the health of a team's backlog, reporting a team's velocity, predictability and say/do across its recent sprints, forecasting a team's throughput, generating work distribution reports showing where completed work went by portfolio, program, project, strategic theme, or work type, managing holiday calendars, team member roles or team types, or listing, creating, updating or closing team risks.
---

# Wayd Teams

Confirm every change with the user before you make it: say what you will create, change or remove, and wait for
their go-ahead.

## Reference

Read the matching file before working in these areas. The rules on this page still apply there.

| File | Read it when |
| --- | --- |
| [reference/changing-teams.md](reference/changing-teams.md) | Creating, updating or deactivating a team or team of teams, changing the team hierarchy, managing members and their roles, or reading, setting, correcting or deleting an operating model |
| [reference/capacity.md](reference/capacity.md) | Reading a team's or team of teams' capacity, capacity report or plan, or proposing allocations |
| [reference/risks.md](reference/risks.md) | Listing, reading, creating, updating, closing or reopening risks, or looking up risk statuses, categories and grades |
| [reference/organization-settings.md](reference/organization-settings.md) | Managing holiday calendars and their holidays, team member roles, or reading team types |

## Team identifiers

A team carries three identifiers, and each tool accepts a specific one:

| Identifier | Example      | Accepted by                                                                                                                                           |
| ---------- | ------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `id`       | UUID         | Every `Teams_*` tool that takes a team except `Teams_GetTeam`; most take only the UUID |
| `key`      | integer      | `Teams_GetTeam` (its parameter is named `id` but takes the integer `key`), `Teams_GetActivities` |
| `code`     | short string | `Teams_GetBacklog`, `Teams_GetWorkItems`, `Teams_GetBacklogHealth`, `Teams_GetThroughputForecast`, `Teams_GetTeamWorkDistribution` (each also takes the UUID) |

Resolve a team name with `Teams_GetTeams`, which returns all three (active teams only unless `includeInactive: true`). A team of teams has the same three; resolve it with `TeamsOfTeams_GetList`. `TeamsOfTeams_GetTeamOfTeams` takes the integer `key`, `TeamsOfTeams_GetActivities` the key or UUID, `TeamsOfTeams_GetWorkDistribution` the UUID or code, and every other `TeamsOfTeams_*` tool the UUID. Most `Teams_*` tools given a team of teams' id answer 404 or an empty list rather than naming the mix-up, so check which kind a record is (`type` in the lists) and use the `TeamsOfTeams_*` tool.

Planning Interval tools take the same UUID `id` as their `teamId`; a team has no separate id within a PI. For which tools, see the `wayd-pi` skill.

## Activity history

`Teams_GetActivities` returns the team's recorded changes, newest first: creation, detail changes, activation and deactivation, membership changes, team-of-teams membership, and operating models.

- Member entries carry employee and role ids, not names. Resolve employee ids with `Employees_GetList` (see the `wayd-employees` skill) or against `employee.id` from `Users_GetUsers`, and role ids with `TeamMemberRoles_GetList` (pass `includeInactive: true`). Report any id you cannot resolve as an id — never guess who or what it is.
- Each `payload` is a JSON string carrying the value before and after the change.
- A `Baseline` entry marks where tracking began for a team that already existed. Nothing before it is in the log, so do not report it as the team's creation.

## A team's work

| Question | Tool | Notes |
| --- | --- | --- |
| The open backlog in rank order | `Teams_GetBacklog` | Requirement-tier items in a Proposed or Active status category, each with its 1-based `rank` |
| Work items, by status category or done date | `Teams_GetWorkItems` | `statusCategories` (Proposed, Active, Done, Removed); `doneFrom`/`doneTo` keep only items done in that window |
| Open dependencies | `Teams_GetDependencies` | Links where the team owns either end and the predecessor (`source`) is not done |
| Every sprint | `Teams_GetSprints` | Each with its `state` (Future, Active, Completed) |
| The sprint in progress | `Teams_GetActiveSprint` | No sprint in progress answers 404: report "no active sprint", not a failure |

These list Requirement-tier work only (user stories, bugs and the like), never epics or features. An unknown team returns an empty list from the backlog, work item and sprint tools, so confirm the team with `Teams_GetTeams` before reporting it has no work. A team of teams has no backlog, work items or sprints of its own; read them per team.

## Backlog health

`Teams_GetBacklogHealth` takes the team's UUID `id` or `code`.

- **Read the outcome before the grade.** A check with outcome `Not Enough History` (fewer than 10 completed work items in the history window) or `Not Applicable` (nothing in scope) has no grade. It is not Healthy — say it could not be assessed.
- **`value` means different things.** Runway is weeks, Net Flow is work items created per completed, WIP Load is active work items per member; for every other check it is the percent of in-scope work items flagged, with `flagged` and `inScope` as the counts.
- **Name the work items.** To explain a problem, filter `workItems` to those whose `flags` include the check, and cite their keys and ranks rather than only the percentage.
- **Thresholds are what-ifs, not settings.** Pass any threshold to see how the grades change; nothing is saved. Quote the `thresholds` in the response when reporting grades, since they may not be the defaults.
- **Readiness checks cover only the top of the backlog.** Missing Estimate, Oversized, No Parent and No Project look at `readinessWindowWorkItems` top-ranked items, not the whole backlog.
- **Estimates are in the team's `sizingMethod`.** `totalEstimate`, `oversizedEstimate` and each work item's `estimate` are Story Points, Effort or Size as the response's `sizingMethod` says — name the unit. A work item with no value in that estimate is missing one even if it has another; 0 is an estimate. For a team that sizes by `Count`, Missing Estimate and Oversized are `Not Applicable`.
- **Rank Inversion compares only dependencies within the team.** Cross-team dependencies are not ranked against each other.

## Sprint metrics

`Teams_GetSprintMetrics` takes the team's UUID `id` only, and returns its velocity, predictability and say/do over its most recent completed standard sprints.

- **Say which sprints the figures cover.** `sprints` lists the sprints counted, most recent first. There may be fewer than `sprintWindow` when the team has too few usable sprints; state the number actually counted.
- **Report the spread with the average.** Each metric has `average`, `min`, `max` and `standardDeviation`. Quote the range (`min` to `max`) beside the average: a wide range means the average is a poor planning figure. A metric is null when no counted sprint has a value for it.
- **Name the unit.** `byEstimate` is in the response's `sizingMethod` (Story Points, Effort or Size); `byCount` counts work items. Under `Count` the two are the same. Never mix them in one statement.
- **Ratios are 0 to 1.** State predictability and say/do as percents. Predictability is velocity ÷ committed, capped at 1, so finished added work stands in for unfinished committed work; say/do counts only the committed items. A team can have high predictability and low say/do.
- **Explain what was left out.** `excluded` lists completed sprints passed over, each with a `reason`: `NonStandard` (not comparable, such as an innovation and planning sprint), `HistoryIncomplete` (work item history not fully synced, so the figures would be unreliable) or `NoCommitment` (nothing was in the sprint at its commitment point). Mention them, since they explain why the window reaches further back.
- **A changed sizing method.** `sizingMethod` is the team's sizing method now. A sprint with `inEstimateAverages: false` was measured in an earlier one. It counts toward `byCount` but not `byEstimate`; do not compare its `byEstimate` values with the others. Each summary's `sprints` says how many sprints it covers, which can be fewer than the sprints listed; when no sprint is in the team's sizing method yet, `byEstimate` is null and `byCount` is the figure to report.
- **Use the organization's window unless asked.** Omit `sprintWindow` to use the organization's setting (`defaultSprintWindow`), which is what Wayd's own pages show. Pass 3 to 12 only when the user asks for another range, and say which you used.
- **An empty report is not an error, a 404 is.** A team with no sprints, or none usable, returns empty `sprints` and null summaries. Say there is nothing to measure, and why from `excluded`. A 404 means the id is not a team's: a mistyped id, or a team of teams, which has no sprints of its own. Do not report it as a team with no sprints.

## Cadence velocity

`Teams_GetCadenceMetrics` takes the team's UUID `id` only, and returns a Kanban team's velocity over its reporting cadence: back-to-back periods of a whole number of weeks set on its operating model. Use it for a Kanban team and `Teams_GetSprintMetrics` for a Scrum team; a cadence has no commitment, so there is no predictability or say/do to report.

- **Say which periods the figures cover.** `periods` lists the current period first (`isCurrent`), then ended ones, most recent first, each with its `start` and `end`. Averages cover only ended, whole periods (`inAverages`); each summary's `periods` says how many.
- **Don't call the current period slow.** Its work is still coming in. A period with `isPartial` was cut short where the cadence, time zone or sizing method changed inside it, so it is shorter than the cadence; neither is averaged.
- **Report the spread with the average,** and name the unit, as for sprint metrics: `byEstimate` is in the response's `sizingMethod`, `byCount` counts work items. A period's `unestimatedCount` is completed work with no value in that unit, which adds nothing to `completedEstimate`; mention it when it is large.
- **Velocity is done work only.** It counts Requirement-tier items that reached a Done-category status in the period, by done date. Removed work is not velocity here.
- **No cadence is not an error.** A team with no cadence returns empty `periods` and a null `cadenceWeeks`: say the team has no cadence set, which its operating model can add. A 404 means the id is not a team's, such as a team of teams.

## Delivery forecasts

Monte Carlo forecasts from each team's recent daily throughput (10,000 trials, history ending yesterday UTC). They need the `delivery-forecasting` feature flag; a 404 on an item you know exists means the flag is off — say so rather than reporting the record missing.

| Question                                                     | Tool                                     | Identifies the work by                                                                   |
| ------------------------------------------------------------ | ---------------------------------------- | ---------------------------------------------------------------------------------------- |
| When will this PI objective's work be done?                  | `PlanningIntervals_GetObjectiveForecast` | `idOrKey` + `objectiveIdOrKey`                                                           |
| When will this project's work be done?                       | `Projects_GetForecast`                   | `idOrKey` (project UUID or key)                                                          |
| How many backlog work items will this team finish by a date? | `Teams_GetThroughputForecast`            | `idOrCode` (team UUID or code) + required `targetDate`                                   |

Forecasting when a single work item will be done lives in the `wayd-work` skill.

- **Read the `outcome` first.** Only `Forecast` fills `percentiles`. `Done`, `Not Enough History` (the team finished fewer than 10 backlog work items in the window), `Blocked by Dependency`, `Cannot Forecast` and `Nothing Remaining` each mean there are no dates — report which, and why from `issues`.
- **Report a range, lead with 85%.** Quote the 85% date (or count) as the planning figure and the 50% as a coin flip; never collapse the forecast to one date. A percentile with a null `date` did not finish within two years — say "beyond 2 years".
- **Chance by target date.** `chanceOfFinishingByTargetDate` is 0 to 1; state it as a percent against `targetDate`. An objective defaults to its target date, else the PI's end; a project to its planned end.
- **Exclusions make it a lower bound.** When `excludedWorkItems` is non-empty, the dates cover only what could be forecast — say so and list the excluded keys with their issue.
- **Name the dependency to chase.** `dependencies[].shareOfTrialsSettingFinish` is how often waiting on that predecessor decided the finish; cite the highest. To show what dependencies cost, compare against `ignoreDependencies: true` and label that result a what-if. `ignoredDependencies` lists links left out (Predecessor Removed, Closes a Cycle) that likely need cleaning up.
- **Backlog position drives the dates.** Everything ahead of an item in its team's backlog counts. By default active items count ahead of proposed ones (`startedWorkFirst`, since teams usually finish what they have started), so an active item can sit ahead of its rank; pass `startedWorkFirst: false` for rank order alone, and say which order a forecast used.
- **Team throughput** answers with a count: each percentile's `workItems` is finished at least that often, and `throughWorkItem` names how far down the backlog that reaches ("at 85%, through CORE-123").
- **History window.** `lookbackDays` (14–365, default 90). Use a shorter window when a team's pace recently changed, and state the window you used. Nothing is saved.

## Work distribution reports

A work distribution report shows where a team's completed work went over a date range.

| Scope                                     | Tool                               | Identifies it by                        |
| ----------------------------------------- | ---------------------------------- | --------------------------------------- |
| One team                                  | `Teams_GetTeamWorkDistribution`    | `idOrCode` (team UUID or code)          |
| A team of teams and every team beneath it | `TeamsOfTeams_GetWorkDistribution` | `idOrCode` (team of teams UUID or code) |

### What counts

Requirement-tier work items (e.g. User Stories, Bugs) whose status category became **Done** between `from` and `to` (`YYYY-MM-DD`, inclusive, UTC, at most 366 days apart). Portfolio-tier items (Epics, Features) and Removed items never count. On a team of teams, each work item counts toward the parent its team had on the day the item became Done.

### Parameters

- **`dimension`** — `Portfolio` (default), `Program`, `Project`, `StrategicTheme`, or `WorkType`. Use the default for both a team and a team of teams unless the user asks for another grouping.
- **`measure`** — `Count` (default; each item counts as 1) or `StoryPoints` (point-sized teams only). A team of teams also offers `TeamEffort`, which measures each team's split in its own sizing method (Story Points, Effort, Size or Count, as it sized on the day the work was done) and combines teams by share of completed items. **For a team of teams, default to `TeamEffort`**: adding story points across teams mixes estimation scales that do not compare.
- **`unestimated`** — with `StoryPoints` or `TeamEffort`: `Exclude` (default) leaves unestimated items out; `TeamAverage` fills them from the team's average for that work type in the same unit. An estimate of 0 is an estimate and is never filled.
- **`themeCounting`** — with `StrategicTheme`: `SplitEvenly` (default) divides a multi-theme project's work so shares total 100%; `CountFully` credits all of it to each theme, so shares exceed 100%.

### Reading the report

- **Start with `summary`.** `itemsCompleted` is the total. Under `StoryPoints`, estimate coverage is `estimatedItems` out of `itemsInPointSizedTeams` — low coverage weakens a point-based report — and `excludedTeams` lists teams the measure left out (such as count-sized teams); name them.
- **Gap groups are findings, not noise.** The `NoProject` group (`kind: "NoProject"`; every dimension except WorkType) is completed work where neither the item nor its parents link to a project; `summary.noProjectShare` is its percent of the total. A high share (above roughly 20–30%) means significant effort escaping portfolio visibility. `MissingLevel` groups (`No program`, `No theme`) are work on a project that lacks a program or theme, kept separate from `NoProject`.
- **Top consumers.** Read `groups` by share. On a team of teams, `largestContributor` names the child team that delivered the most for each group.
- **Team breakdown.** On a team of teams, `teams` shows each child team's contribution — look for teams heavy in `No project` or dedicated to a single initiative.
