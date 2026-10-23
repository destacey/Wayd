# Changing teams and teams of teams

Every tool here changes a record other people rely on. Say what you will change and get the user's go-ahead
before each call. These tools are offered by the hosted MCP endpoint (`/mcp`) only.

## Creating, updating and deactivating

| Task | Team | Team of teams |
| --- | --- | --- |
| Create | `Teams_Create` | `TeamsOfTeams_Create` |
| Rename, recode or redescribe | `Teams_Update` | `TeamsOfTeams_Update` |
| Deactivate | `Teams_Deactivate` | `TeamsOfTeams_Deactivate` |

- **`name` and `code` are each unique across teams and teams of teams.** A code is 2 to 10 uppercase letters and
  digits. Create returns both the UUID `id` and the integer `key`.
- **A new team starts Kanban, sized by Count**, in the system default time zone and working week. If the user
  wants Scrum or story points, set an operating model afterwards (see below). A new team of teams starts with the
  system default time zone.
- **Update is a whole-record overwrite of name, code and description.** An omitted description is cleared: read
  the record first (`Teams_GetTeam` / `TeamsOfTeams_GetTeamOfTeams`, both by integer key) and pass back what
  should stay. The body's `id` must match the path `id` (the UUID).
- **Deactivation cannot be undone through any tool.** The `inactiveDate` must be after the active date. It is
  refused while the team's latest membership in a team of teams is open-ended or ends after that date; a team
  of teams is also refused while the most recent membership of a team beneath it does. End those memberships
  first, then deactivate.

## The hierarchy: team-of-teams memberships

A team or team of teams belongs to at most one parent team of teams on any day, through dated memberships. The
hierarchy decides team-of-teams rollups (capacity, work distribution) and the functional organization chart for
each date.

| Task | Tool |
| --- | --- |
| A team's memberships | `Teams_GetTeamMemberships` |
| A team of teams' memberships (its own parent first, then the teams directly beneath it) | `TeamsOfTeams_GetTeamMemberships` |
| Put a team under a team of teams | `Teams_AddTeamMembership` |
| Nest a team of teams under another | `TeamsOfTeams_AddTeamMembership` |
| Change a membership's dates | `Teams_UpdateTeamMembership` / `TeamsOfTeams_UpdateTeamMembership` |
| Delete a membership | `Teams_RemoveTeamMembership` / `TeamsOfTeams_RemoveTeamMembership` |
| The whole tree on a date | `Teams_GetFunctionalOrganizationChart` |

- **Memberships are changed through the child.** The path `id` is always the child's UUID and must match the
  body's `teamId`; `parentTeamId` is the parent team of teams' UUID. `teamMembershipId` is the membership's own
  `id` from the get tools.
- **Ranges cannot overlap.** To move a team to another parent, end its current membership
  (`*_UpdateTeamMembership` with an `end`), then add the new one starting the day after.
- **Update overwrites both dates.** Omitting `end` makes the membership open-ended; pass the current `end` back
  to keep it.
- **Prefer ending over removing.** Removing deletes the membership for its whole range, so the team vanishes from
  the parent's history as well. Remove only a membership added by mistake.
- Both teams must be active for any membership change, and a team of teams cannot be nested under one of its own
  descendants.

## Members

Members are the people on a team, each holding one or more team member roles. Membership has no dates and does
not give the team any of the person's time: time is an allocation, changed with `Employees_ChangeAllocations`
(see the `wayd-employees` skill).

| Task | Team | Team of teams |
| --- | --- | --- |
| List members and their roles | `Teams_GetMembers` | `TeamsOfTeams_GetMembers` |
| Add a member | `Teams_AddMember` | `TeamsOfTeams_AddMember` |
| Change a member's roles | `Teams_UpdateMember` | `TeamsOfTeams_UpdateMember` |
| Remove a member | `Teams_RemoveMember` | `TeamsOfTeams_RemoveMember` |

- `employeeId` is the employee UUID (`employee.id` in the member list, or from `Employees_GetList`). `roleIds` are
  team member role UUIDs from `TeamMemberRoles_GetList`.
- **Add** needs at least one role, an active team and an active employee. Adding a current member adds the
  roles; a role they already hold on that team is refused.
- **Update replaces the member's roles.** Roles left out are removed, so read `*_GetMembers` first and send every
  role they keep. At least one role; to take someone off the team, use `*_RemoveMember`.
- **Remove** takes away every role the person holds there. It does not touch their allocations.
- A team of teams' members are only the people on it directly, not the members of its teams.

## Operating models

An operating model is how a team works from a date: `methodology` (`Scrum` or `Kanban`), `sizingMethod`
(`StoryPoints`, `Count`, `Effort` or `Size`), IANA `timeZone`, `commitmentGraceDays` (0 to 14), `workingDays`,
`holidayCalendarId` (null for the system default calendar), and for Kanban an optional reporting cadence.
Models run back to back; exactly one is current. A team of teams' model holds only a time zone.

| Task | Team | Team of teams |
| --- | --- | --- |
| History | `Teams_GetOperatingModels` | `TeamsOfTeams_GetOperatingModels` |
| One model | `Teams_GetOperatingModel` | `TeamsOfTeams_GetOperatingModel` |
| In force on a date (default today) | `Teams_GetOperatingModelAsOf` | — |
| In force for several teams on a date | `Teams_GetOperatingModelsForTeams` | — |
| Pre-filled values for a new model | `Teams_GetOperatingModelDefaults` | `TeamsOfTeams_GetOperatingModelDefaults` |
| Has the team ever been Scrum? | `Teams_HasEverBeenScrum` | — |
| Change how it works from a date | `Teams_SetOperatingModel` | `TeamsOfTeams_SetOperatingModel` |
| Fix a model's values for its whole period | `Teams_UpdateOperatingModel` | `TeamsOfTeams_UpdateOperatingModel` |
| Undo the current model | `Teams_DeleteOperatingModel` | `TeamsOfTeams_DeleteOperatingModel` |

- **Set or correct — ask which the user means.** `*_SetOperatingModel` starts a new model on `startDate` (after
  the current model's start) and closes the current one the day before: use it when the team really changed how
  it works from a date. `*_UpdateOperatingModel` rewrites one model, past or current, for every date it covers:
  use it to fix a value that was wrong all along. Correcting a past model changes what reports read for that
  period.
- **Start from the defaults.** `Teams_GetOperatingModelDefaults` gives the time zone (the parent team of teams'
  on that date, else the system default), the commitment grace period and the working week to propose when the
  user has not said.
- **Correcting is a whole-record overwrite.** An omitted `holidayCalendarId` reverts to the system default and an
  omitted cadence is removed; only an omitted `workingDays` keeps the model's own. Read the model first and pass
  back every value that should stay. Setting a new model with `workingDays` omitted carries the current model's
  working week over.
- **Cadence is Kanban only.** `cadenceWeeks` (1 to 8) and `cadenceAnchor` (yyyy-MM-dd, the first day of one
  period) go together; omit both for no cadence, and always for Scrum. A Kanban team's cadence is what
  `Teams_GetCadenceMetrics` measures over.
- **Delete only undoes the current model,** and never the last one: the previous model becomes current again.
- Changing `sizingMethod` changes the unit of the team's estimates from then on; say so, since sprint metrics and
  backlog health report in the current sizing method.
