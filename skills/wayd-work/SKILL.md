---
name: wayd-work
description: Read before calling the Wayd MCP tools named Workspaces_*, Sprints_*, WorkProcesses_*, WorkStatuses_*, WorkStatusCategories_*, WorkTypes_*, WorkTypeLevels_* or WorkTypeTiers_*. Guides agents working with Wayd work management via the Wayd MCP server — workspaces and the work items synced into them, team sprints and their lifecycle, and the work processes, work types, levels, tiers and statuses that classify work. Use when finding or searching work items, reading a work item's children, dependencies, progress or project, setting a portfolio work item's project, forecasting when a work item will be done, listing a team's sprints, reading a sprint's backlog, scope, burn-up and burn-down, metrics or capacity, starting, completing or reopening a sprint, correcting its actual dates, team days off or type, or looking up and configuring work types, work type levels, work statuses and work processes.
---

# Wayd Work (Workspaces / Work Items / Sprints / Work Configuration)

These tools are offered by the hosted MCP endpoint (`/mcp`), in its `work` toolset. `Workspaces_GetWorkItemForecast` is the exception: the `@wayd/mcp` package offers it too; every other tool here is hosted-only.

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, sets, starts, completes, reopens, corrects, reorders, activates or deactivates anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up stays undone unless they ask for it.

## What syncs, and what Wayd records

Most of this area is a copy of the systems teams already track work in (Azure DevOps), kept current by background sync. A record with `ownership` Managed comes from there and is read-only in Wayd: a change to it belongs in the source system, and no tool here creates or edits a workspace, a work item, a sprint's name or planned dates, or a workflow.

| Synced from the source system | Recorded in Wayd, through these tools |
| --- | --- |
| Workspaces (one per Azure DevOps project) and their work items: title, type, status, parent, team, sprint, assignee, estimates and dependency links | A Portfolio-tier work item's project (`Workspaces_UpdateWorkItemProject`) and a workspace's link back to the source (`Workspaces_SetExternalUrlTemplates`) |
| Sprints: name, planned start and end, and team | When the team actually started and completed a sprint, its team days off and its sprint type |
| Work processes, the work types and workflows in them, and work statuses | A work type's level and description, the work type levels and their order, status descriptions, and whether a work process is active |

## Identifiers

Each tool takes a specific identifier; its parameter description says which. The ones to get right:

| Record | Identifiers | Watch for |
| --- | --- | --- |
| Workspace | UUID `id`, or `key` (2–20 uppercase letters and digits, starting with a letter: `CORE`) | `idOrKey` takes either. `Workspaces_SetExternalUrlTemplates` and `Workspaces_UpdateWorkItemProject` take the UUID `id` only. |
| Work item | `key` (`CORE-123`: the workspace key, then the item's id in the source system), and a UUID `id` | Work item tools take the workspace as `idOrKey` — the key's prefix works — plus `workItemKey`. Only `Workspaces_UpdateWorkItemProject` takes the UUID (`workItemId`); read it from `Workspaces_GetWorkItem`. |
| Sprint | UUID `id`, or integer `key` | Read tools' `idOrKey` takes either. `Sprints_Start`, `Sprints_Complete`, `Sprints_Reopen`, `Sprints_SetTeamDaysOff`, `Sprints_SetSprintType` and `Sprints_CorrectActualDates` take the UUID. `Sprints_GetPlanningIntervals` takes the integer `key` only. |
| Team | UUID `id` | `Sprints_GetSprints` filters by `teamId`. Resolve a team name with `Teams_GetTeams` (see the `wayd-teams` skill). |
| Work process | UUID `id`, or integer `key` | `WorkProcesses_Activate`, `WorkProcesses_Deactivate` and `WorkProcesses_GetSchemes` take the UUID only. |
| Work type, work type level, work status | integer `id` | Names are unique; resolve them with the list tools. |

## Finding work

| Goal | Tool |
| --- | --- |
| List workspaces (active only unless `includeInactive: true`) | `Workspaces_GetWorkspaces` |
| One workspace, its work process and URL template | `Workspaces_GetWorkspace` |
| Find a work item by part of its key, its parent's key or its title, across every workspace | `Workspaces_SearchWorkItems` (`query`; at most `top`, default 50) |
| One work item's details | `Workspaces_GetWorkItem` |
| A Portfolio-tier item's direct children, in rank order | `Workspaces_GetChildWorkItems` |
| A work item's dependency links | `Workspaces_GetWorkItemDependencies` |
| Daily Proposed / Active / Done counts of the Requirement-tier work under an item | `Workspaces_GetWorkItemMetrics` |
| Where a work item's project comes from | `Workspaces_GetWorkItemProjectInfo` |
| When a work item will be done | `Workspaces_GetWorkItemForecast` (see [Forecasting a work item](#forecasting-a-work-item)) |
| Every work item in a workspace | `Workspaces_GetWorkItems` — can be thousands of items; search, or use a team's or project's work item tools, when you want particular ones |

- **Dependencies are read from this item's side.** `type` Predecessor means this item waits on `dependency`; Successor means `dependency` waits on this item.
- **Tiers decide what an item can be.** Only Portfolio-tier items (Epics, Features) have children and take a project; Requirement-tier items (stories, bugs) are a team's backlog and sprint work, and what metrics, scope and forecasts count. See [Work configuration](#work-configuration).

## Setting a work item's project

`Workspaces_UpdateWorkItemProject` sets a **Portfolio-tier** work item's own project; any other tier is refused. Read `Workspaces_GetWorkItemProjectInfo` first and tell the user what the item has now: `projectSource` WorkItem (its own), Parent (inherited, `parentProject`) or None.

- A `projectId` sets the item's own project, overriding the one it inherits. Resolve project ids with the `wayd-ppm` skill's tools.
- A null `projectId` removes the item's own assignment, so it inherits its parent's project again. Choosing the parent's project is the same as inheriting it.
- Work items beneath it that have no project of their own inherit the change — say so when confirming.
- It takes the workspace's UUID `id` and the work item's UUID twice, as `workItemId` in the path and in the body, which must match. A work item that is not in that workspace is not found. It needs the `Permissions.Projects.ManageProjectWorkItems` permission.

## Sprints

A sprint is one team's time-box. Its name, team and **planned** dates sync from the source system; the team records in Wayd when it **actually** started and completed it, and nothing is written back.

`state` (Future, Active, Completed) is worked out each time it is read: a sprint is Active from its recorded start, or else the start of its first planned day, until its recorded completion, or else the end of its last planned day, in the team's time zone. A team has one Active sprint at a time.

### Reading a sprint

| Question | Tool |
| --- | --- |
| A team's sprints | `Sprints_GetSprints` with `teamId` — always pass it when the team is known; without it every team's sprints come back |
| Dates, state, type, team days off, and what the caller may do now | `Sprints_GetSprint` |
| The work items in the sprint **now** | `Sprints_GetBacklog` |
| Counts and estimates of the work in the sprint **now** (done, in progress, not started) | `Sprints_GetMetrics` |
| What the sprint **committed to** and what became of it — committed or added; completed, removed, carried over or descoped | `Sprints_GetScope` |
| Scope and completed work day by day | `Sprints_GetBurn` |
| The team's capacity beside the sprint's scope | `Sprints_GetCapacity` (feature-gated) |
| Its change history: syncs, starts, completions, corrections | `Sprints_GetActivities` |
| Which planning intervals it is mapped to | `Sprints_GetPlanningIntervals` (integer `key`) |
| Velocity, predictability and say/do across a team's recent sprints | `Teams_GetSprintMetrics` (the `wayd-teams` skill) |

- **Now versus history.** Backlog and metrics describe the sprint as it is today; scope and burn rebuild it from work item history between its commitment point and its end. For "did the team deliver what it committed to", use scope, not metrics.
- **The commitment point** is when the team started the sprint, or, if it never pressed Start, the end of the team's commitment grace period after the planned start. Work added before it counts as committed.
- **Scope, burn and capacity need planned dates**, and capacity needs a team: a 404 from them on a sprint you just read means it has none.
- **Name the unit.** Estimates are in the response's `sizingMethod` (Story Points, Effort or Size; Count counts items).

### Changing a sprint

Read `Sprints_GetSprint` first. It says whether the caller may act (`canManageSprint`), whether the lifecycle allows each step now (`canStart`, `canComplete`, `canReopen`), the moments a start or completion may be recorded at (`startWindow`, `completeWindow`), and `openSprint`, another of the team's sprints still open.

| Tool | Records | Allowed when |
| --- | --- | --- |
| `Sprints_Start` | The actual start: now, or an earlier `startedAt` inside `startWindow` | After the team's previous sprint started, up to the end of this sprint's last planned day and before the next sprint's start. The team's first sprint can start up to 3 days before its planned start. |
| `Sprints_Complete` | The actual end: now, or an earlier `completedAt` inside `completeWindow` | After the sprint started (its commitment point having passed is enough), before a later sprint has started, and once any earlier sprint the team left open has been completed. |
| `Sprints_Reopen` | Clears the actual end | While the team has not started a later sprint and, for a sprint the team started, while no other sprint of the team is open. |
| `Sprints_CorrectActualDates` | Replaces the actual start and end of one or more of a team's sprints, past or current | Starts and ends keep the team's sprints in order and never overlapping; neither may be in the future. |
| `Sprints_SetTeamDaysOff` | Replaces the days in the sprint the whole team is off (an offsite), each with a `percentOff` | Days within the sprint's planned dates, each listed once. |
| `Sprints_SetSprintType` | Sets the sprint's type, or clears it with null | Any time. |

- **One open sprint per team.** When `openSprint` is set, `Sprints_Start` is refused unless `completeOpenSprintId` names that sprint, which completes it at the same moment the new one starts. Ask the user before passing it — it completes another sprint.
- **Times are instants** (`2026-10-05T09:00:00Z`). A recorded start or end can never be in the future; omit it to record "now".
- **Who can.** Members of the sprint's team, or of the team of teams it belongs to, holding `Permissions.Iterations.Update`; holders of `Permissions.Iterations.Administer` can act on any team's sprints. The caller must be linked to an employee. A sprint whose source team is not mapped to a Wayd team cannot be started or completed. When a call is refused, check `canManageSprint` before retrying.
- **Replacements, not edits.** `Sprints_SetTeamDaysOff` replaces the whole list: send every day that should remain, read from the sprint's `teamDaysOff`; an empty list clears them. `Sprints_CorrectActualDates` replaces both `started` and `completed` of each sprint named: send the current value of the one you are not changing, or it reverts to the planned date. To move one sprint's end past the next sprint's start, correct both sprints in the same call.
- **Holidays and non-working days are already off.** A team day off is for a day no calendar knows; time away for one person is recorded on the employee (the `wayd-employees` skill), not here.
- **Sprint type.** `NonStandard` leaves a sprint out of the team's sprint metrics (an innovation and planning sprint, a hackathon, a holiday sprint); its own metrics still show. Without a type set by the team, a sprint follows the category of the planning interval iteration it is mapped to, else Standard. `sprintTypeSource` says which applies. `Sprints_GetSprintTypes` lists the codes.
- **Corrections move metrics.** Correcting actual dates moves the commitment point, which changes the sprint's scope and metrics. Say so when confirming.

## Forecasting a work item

`Workspaces_GetWorkItemForecast` answers "when will this work item be done?" by Monte Carlo simulation of its team's recent daily throughput (10,000 trials, history ending yesterday UTC). Identify the item by `idOrKey` (the workspace key — the prefix of the work item key) and `workItemKey` (`CORE-123`). A Portfolio-tier item (an Epic or Feature) is forecast from its open backlog descendants. For a PI objective, a project or a team's throughput, see the `wayd-pi`, `wayd-ppm` and `wayd-teams` skills.

- **Read the `outcome` first.** Only `Forecast` fills `percentiles`. `Done`, `Not Enough History` (the team finished fewer than 10 backlog work items in the window), `Blocked by Dependency`, `Cannot Forecast` and `Nothing Remaining` each mean there are no dates — report which, and why from `issues` (No Team, Not a Backlog Item, Not Enough History).
- **Report a range, lead with 85%.** Quote the 85% date as the planning figure and the 50% as a coin flip; never collapse the forecast to one date. A percentile with a null `date` did not finish within two years — say "beyond 2 years".
- **Chance by a date.** A work item has no target date of its own: pass `targetDate` (`YYYY-MM-DD`) to get `chanceOfFinishingByTargetDate`, 0 to 1, and state it as a percent.
- **Backlog position drives the date.** Everything ahead of the item in its team's backlog counts; report `backlogPosition` with the date. By default active items count ahead of proposed ones (`startedWorkFirst`); pass `startedWorkFirst: false` for rank order alone, and say which order you used.
- **Exclusions make it a lower bound.** When `excludedWorkItems` is non-empty, the dates cover only what could be forecast — say so and list the excluded keys with their issue.
- **Name the dependency to chase.** `dependencies[].shareOfTrialsSettingFinish` is how often waiting on that predecessor decided the finish; cite the highest. To show what dependencies cost, compare with `ignoreDependencies: true` and label that a what-if. `ignoredDependencies` lists links left out (Predecessor Removed, Closes a Cycle).
- **History window.** `lookbackDays` (14–365, default 90). Use a shorter window when the team's pace recently changed, and state the window you used. Nothing is saved.

## Work configuration

Work configuration classifies work items so they compare across teams and tools. Most of it syncs with the work process; Wayd adds the hierarchy.

- **Work process** — the set of work types a workspace's items can have and the workflow each follows. Synced; `WorkProcesses_GetSchemes` lists each work type with its workflow. Only an active work process can be assigned to a workspace, and `WorkProcesses_Deactivate` is refused while an active workspace uses it.
- **Work status** — a state such as New or In Progress, reusable across workflows. A status has no category of its own: each workflow places it in a **work status category**, the fixed set Proposed, Active, Done and Removed (`WorkStatusCategories_GetWorkStatusCategories`). Metrics, scope and forecasts read the category, never the status name.
- **Work type** — a kind of work item (Epic, User Story, Bug). Its **level** sets its **tier** (`WorkTypeTiers_GetWorkTypeTiers`): Portfolio (containers; the only tier that has children, takes projects and has several levels), Requirement (a team's backlog and sprint work), Task (work owned by a parent item) and Other (left out of backlog and sprint views). Work types synced from Azure DevOps start in the Other tier until someone gives them a level.
- **Work type level** — orders work types within a tier, 1 at the top: a Portfolio-tier parent must sit at a higher level than a Portfolio-tier child (Epics above Features).

| Change | Tool | Note |
| --- | --- | --- |
| Put a work type in the hierarchy | `WorkTypes_Update` with `levelId` | Moves it to that level's tier, which changes where all its work items appear and what metrics count them. Its name cannot change. |
| Add a Portfolio level (a Theme above Epics, say) | `WorkTypeLevels_Create`, then `WorkTypeLevels_Reorder` | Only the Portfolio tier takes new levels; a new one goes to the bottom. |
| Rename a level, or change its description | `WorkTypeLevels_Update` | Any tier. Names are unique. |
| Reorder Portfolio levels | `WorkTypeLevels_Reorder` | `levels` maps level id to position: `{ "12": 1, "14": 2 }`. Positions must stay unique. |
| Change a status's description | `WorkStatuses_Update` | Its name cannot change. |
| Turn a work process on or off | `WorkProcesses_Activate`, `WorkProcesses_Deactivate` | |

- **Updates are whole-record overwrites.** `WorkTypes_Update`, `WorkTypeLevels_Update` and `WorkStatuses_Update` rewrite the record from the body: an omitted `description` is cleared. Read the record first and pass back what should stay, with the same `id` in the path and the body.
- **Creating work types and statuses is rarely needed.** Syncing a work process creates the ones it brings, reusing an existing record of the same name. Create one by hand only when the user asks, and remember names can never change afterwards.

## Feature-gated tools

`Workspaces_GetWorkItemForecast` needs the delivery-forecasting feature flag, and `Sprints_GetCapacity` the team-capacity-management flag. With the flag off, each answers 404: when the record exists, say the feature is turned off rather than reporting the record missing.
