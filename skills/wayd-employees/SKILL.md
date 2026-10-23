---
name: wayd-employees
description: Read before calling the Wayd MCP tools named Employees_*. Guides agents working with Wayd employees — the person records behind role and assignee fields — via the Wayd MCP server, including reporting lines, team memberships, assigned work, time away, holiday calendars, working day hours and how an employee's time is allocated to teams. Use when looking up an employee or their manager and direct reports, creating, updating or removing an employee record, listing what an employee is working on or which teams they belong to, recording or reviewing time away, or changing an employee's allocations, holiday calendar or working day.
---

# Wayd Employees

The `Employees_*` tools are offered by the hosted MCP endpoint (`/mcp`) only, in its `organization` toolset.

For the person asking ("my teams", "my tickets", "my allocations"), use the `Me_*` tools in the `wayd-users` skill instead: they take no id, and reading your own teams and allocations needs no View Employees permission, which the matching `Employees_*` reads do.

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, records, sets, moves, removes or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up stays undone unless they ask for it.

## Employees versus users

An **employee** is the person record: the one role lists (`sponsorIds`, `ownerIds`, `managerIds`, `memberIds`), task `assigneeIds`, work item assignees, team memberships and allocations point at. A **user** is a sign-in account (see the `wayd-users` skill). An employee may have no user, and creating an employee creates no account.

Most employees are synced from a people connector (such as Microsoft Entra ID). An edit to a synced employee is overwritten from the source system at the next sync, so a lasting change to a name, title, department or manager belongs in that system.

## Identifiers

An employee has a UUID `id` and an integer `key`. `Employees_GetEmployee` takes either; every other employee tool takes the UUID only.

To resolve names, call `Employees_GetList` once and filter the result — don't call `Employees_GetEmployee` per person. It returns active employees only unless `includeInactive: true`. If several match a name, ask which one. If `Employees_GetList` is denied (it needs the Employees view permission), `Users_GetUsers` also carries each user's `employee.id`.

## Reporting lines

Each employee's `manager` is another employee. `Employees_GetDirectReports` returns one level only — the active employees whose manager this is. To walk a whole reporting tree, call it again for each report, or build the tree from the `manager` of every row in one `Employees_GetList` call, which is cheaper.

## Changing an employee record

- **`Employees_Update` is a whole-record overwrite.** Read the employee with `Employees_GetEmployee` first and send back every field that should stay; an omitted `managerId` removes the manager. It never changes whether the employee is active.
- **`Employees_Create`** adds an active record by hand. A `managerId` that matches no employee is refused, as it is by `Employees_Update`.
- **Removing a record.** `Employees_RemoveInvalid` is for a synced record that is not a real person (a shared mailbox, a service account): it clears the manager of its direct reports, deletes it and blocks its employee number so the connector never brings it back, and no tool lifts the block. `Employees_Delete` deletes the record outright, and the connector recreates it if it still syncs that person. Tell the user which one you mean and why.

## Time away

Time away lowers an employee's availability to every team they are allocated to, for `percentAway` (1–100) of each day from `start` to `end`. It needs the team-capacity-management feature flag; every time away, allocation, holiday calendar and working day tool returns 404 when the flag is off. An unknown employee id is also a 404 on the reads and on the allocation, holiday calendar and working day writes; the time away writes (record, change, remove) report it as 404 only to a time-away administrator, and anyone else gets the same refusal as for an employee they may not manage, so a refusal there does not prove the employee exists. Before reporting a 404 or a refusal, confirm the employee with `Employees_GetEmployee`: if it exists, a 404 means the flag is off.

**The reason someone is away is private.** Only the employee, anyone above them in their manager chain (at any depth), and holders of the Administer Employee Time Away permission may see or set it:

- `Employees_GetTimeAway` returns `canManage` for the caller. When it is false, every stretch's `reason` is null. Report the dates and share of each day only; never guess, infer or describe why the person is away, and never present a null reason as "no reason".
- Never carry a reason you were shown into anything another person reads — a team summary, a capacity report, a message, another record. Say the person is away, not why.
- Recording, changing and removing time away (`Employees_RecordTimeAway`, `Employees_ChangeTimeAway`, `Employees_RemoveTimeAway`) are allowed for the same people and need no permission otherwise; the caller must also be linked to an employee. Anyone else is refused, whether or not the employee exists, so when `canManage` is false, tell the user who can record it rather than trying.

`Employees_GetTimeAwayReasons` lists the reason codes (`PaidTimeOff`, `BusinessTravel`, `Training`, `Other`). `Employees_ChangeTimeAway` is a whole-record overwrite of the stretch: send `start`, `end`, `percentAway` and `reason`, keeping the values that should stay. Stretches may overlap, but no day can add up to more than a whole day away.

## Capacity

- **Allocation** is the share of an employee's time each team (or team of teams) has, by role, over date ranges; it never exceeds 100% on any day. `Employees_GetAllocations` lists them with `canManage` per team.
- **`Employees_ChangeAllocations`** sets what a team and role have over a date range, splitting longer allocations; `percent: 0` ends it for the range. All changes apply together or not at all.
- **`Employees_MoveAllocation`** moves a share from one team and role to another in one step — use it for a fully allocated person, where raising the target first would exceed 100%.
- Changing allocations needs the Teams update permission and membership of every team named (or its team of teams), unless the caller holds Administer Teams.
- **Membership is not allocation.** `Employees_GetTeamMemberships` lists the teams a person belongs to and their roles there (404 for an unknown employee, an empty list for one on no team); a member may have no allocation, and an allocation needs no membership.
- **Holiday calendar and working day.** `Employees_ChangeHolidayCalendar` sets the shared calendar a person follows (null for the system default); `Employees_ChangeWorkingDayHours` sets a shorter or longer day, such as 4 for a part-timer (null for the system's). Both apply on every team. `Employees_GetTimeAway` shows the current calendar and working day.

## Tools by task

| Task                                                  | Tool                                                                    |
| ----------------------------------------------------- | ----------------------------------------------------------------------- |
| Find employees, resolve names to ids                  | `Employees_GetList`                                                     |
| One employee's details                                | `Employees_GetEmployee` (UUID or key)                                   |
| Direct reports                                        | `Employees_GetDirectReports`                                            |
| Teams an employee belongs to                          | `Employees_GetTeamMemberships`                                          |
| Work items assigned to an employee                    | `Employees_GetWorkItems` (backlog-level items; filter by status category or done window) |
| Create, update, delete an employee                    | `Employees_Create`, `Employees_Update`, `Employees_Delete`               |
| Remove a synced record that is not a person           | `Employees_RemoveInvalid`                                               |
| Time away                                             | `Employees_GetTimeAway`, `Employees_GetTimeAwayReasons`                 |
| Record, change, remove time away                      | `Employees_RecordTimeAway`, `Employees_ChangeTimeAway`, `Employees_RemoveTimeAway` |
| Allocations to teams                                  | `Employees_GetAllocations`, `Employees_ChangeAllocations`, `Employees_MoveAllocation` |
| Holiday calendar, working day hours                   | `Employees_ChangeHolidayCalendar`, `Employees_ChangeWorkingDayHours`    |

## Key rules

- Role and assignee fields take the employee `id`, never a user id.
- Read before every overwrite (`Employees_Update`, `Employees_ChangeTimeAway`) and send back what stays.
- A time-away reason is shown only to the employee, their manager chain and time away administrators; never reveal, infer or pass it on.
- A 404 from a capacity or time away tool on an employee you know exists means the team-capacity-management flag is off; otherwise a 404 from an employee tool means no employee has that id. A time away write refuses a non-administrator the same way whether or not the employee exists.
