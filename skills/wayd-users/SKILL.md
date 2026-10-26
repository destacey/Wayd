---
name: wayd-users
description: Read before calling the Wayd MCP tools named Users_* or Me_*, and before putting a person into any role or assignee field. Guides agents working with Wayd users via the Wayd MCP server, finding out who the person asking is, and resolving a person's name to the employee id that role and assignee fields require. Use when a request is about the person asking (my tickets, my teams, projects I manage, my allocations), when looking up a user, or when a project, program, or portfolio role list (sponsorIds, ownerIds, managerIds, memberIds), a task's assigneeIds, or an activity-history payload needs a person matched to an id.
---

# Wayd Users

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

## User id versus employee id

A user is a sign-in account; an employee is the person record that holds roles and work. Every role and assignee field in Wayd takes an **employee id**, never a user id:

- Project, program, and portfolio role lists: `sponsorIds`, `ownerIds`, `managerIds`, `memberIds`
- Task `assigneeIds`
- `employeeId` filters on the project tools
- People in most activity-history payloads — identity events (users created, locked out, linked to an employee) carry user ids instead

A user `id` in any of these does not identify the person.

For anything about the employee record beyond resolving a name to its id — reporting lines, team memberships, assigned work, time away, allocations — see the `wayd-employees` skill.

To give someone a role on the hosted server, append their employee id with a patch tool — `Projects_Patch` (or `Programs_Patch`, `Portfolios_Patch`) with `[{ "op": "add", "path": "/managerIds/-", "value": "<employeeId>" }]`, or `Tasks_PatchProjectTask` with `/assigneeIds/-` — which keeps everyone already in the role. Without a patch tool, follow wayd-ppm's update checklist.

The exception is a story map's owner: `StoryMaps_ChangeStoryMapOwner` takes the user `id`.

## Questions about yourself

When the request is about the person asking — "show me my tickets", "which teams am I on", "where am I the project manager" — never list users or employees to find them. Those lists need the Users or Employees view permission, which most people do not hold, and nobody needs it to see their own records.

On the hosted endpoint, the `Me_*` tools act for the caller and take no id:

| Question | Tool |
| --- | --- |
| Who am I, and which employee am I? | `Me_GetProfile` — `employee.id` is the id employeeId, role and assignee parameters take for you; `userId` is for parameters that say they take a user id |
| Which teams am I on? | `Me_GetTeamMemberships` — membership and the member roles you hold |
| How is my time split across teams? | `Me_GetAllocations` (team-capacity-management flag) |
| What are my tickets? | `Me_GetWorkItems` — pass `statusCategories` `Proposed` and `Active` for open work |
| Where am I the project manager (or sponsor, owner, member)? | `Projects_GetProjects` with `role` and no `employeeId`: a role filter defaults to the caller |
| My projects at a glance, my project tasks | `Projects_GetMyProjectsSummary`, `Projects_GetMyProjectsTaskMetrics` |
| Risks assigned to me | `Risks_GetMyRisks` |
| What do I follow? What needs a look? Follow or unfollow this project | `Me_GetFollows` (with `view`), `Me_GetFollowCounts`, `Me_GetFollowedRecord`; `Me_Follow` / `Me_Unfollow` (following feature flag) |
| What changed on the things I care about? Do I have anything unread? | `Me_GetNotifications` (with `unreadOnly`), `Me_GetUnreadNotificationCount`; mark read with `Me_MarkRecordNotificationsRead` or `Me_MarkAllNotificationsRead` (following feature flag) |
| Stop telling me about this project. Why don't I hear about X? What have I muted? | `Me_MuteRecord` / `Me_UnmuteRecord`, `Me_MuteReason` / `Me_UnmuteReason`; check with `Me_GetRecordMuteStatus`, list with `Me_GetMutedRecords` and `Me_GetReasonMutes` (following feature flag) |

### Following

`Me_Follow`, `Me_Unfollow` and `Me_GetFollowStatus` take an `ownerType` — `ppm.project`, `ppm.program` or `planning.planning-interval-objective` — and the record's UUID as `ownerId`, never its key: look the record up first. Following acts only for the caller; there is no way to make someone else a follower, so do not offer one. It grants nothing: a record the caller cannot read answers 404 rather than being followed, and drops out of `Me_GetFollows` once they lose access. Following twice and unfollowing something not followed both succeed without changing anything. Following a record puts its changes in the follower's notifications; it sends no email or message.

`Me_GetFollows` is paged and lists every kind together, most recently changed first; `sort` (`LastChanged` or `Followed`) and `direction` (`Descending` or `Ascending`) change the order, so `direction: Ascending` finds the records that have gone quiet longest, and `search` matches a name or an exact key. For "what needs my attention", pass `view`: `Unhealthy`, `ChangedRecently` (the last seven days) or `Closed` (finished, the ones worth unfollowing); `Me_GetFollowCounts` gives every count in one call. To say what changed on one record since the user followed it, use `Me_GetFollowedRecord`, whose `changesSinceFollowed` is that record's activity.

### Notifications

`Me_GetNotifications` lists one entry per record, newest change first: the record, the newest `event` (`StatusChanged`, `HealthReported`, `TimelineChanged`, `RolesChanged` and so on), who made it (`actor`), and `reasons` — `Following` for a record the user follows, `Participant` for one they hold a role on or whose team owns it. `count` and `unreadCount` say how many changes the entry collapses; for the detail of each, read the record's activity. Nobody is told about their own changes or about imports, and a record the user can no longer read is left out, so never infer that a missing entry means nothing happened. The list pages by cursor: for more, pass the response's `nextCursor` as `before`, and stop when it is null. If a result was cut for length, the cursor still points past the entries that were cut, so ask again with a smaller `pageSize` rather than following it. `Me_GetUnreadNotificationCount` counts records, not changes, and stops at 99 (`isCapped`). Mark read only when the user asks: `Me_MarkRecordNotificationsRead` for one record (by `ownerType` and UUID) — prefer it, since an entry is the whole record — `Me_MarkNotificationRead` for just an entry's newest notification (its `latestNotificationId`), `Me_MarkAllNotificationsRead` for everything; reading the list does not mark anything read. Notifications are in-app only: there is no email, Teams or push.

### Muting

A user can mute a record (`Me_MuteRecord`, by `ownerType` and UUID) to hear nothing more about it for any reason, or mute a reason (`Me_MuteReason` with `Following` or `Participant`) to stop being told for that reason on every record; any other reason answers 422. Mute only when the user asks, and only for themselves — there is no way to mute for someone else. A muted reason removes only that reason: someone who follows a project and takes part in it is still told about it after muting `Participant`. Muting changes nothing else: the user keeps their access and roles, still follows what they followed, and a record they cannot read answers 404 rather than being muted. Muting a record also marks its notifications read. `Me_MuteRecord` answers `wasAlreadyMuted`; if it is true the user had muted the record before, so never unmute it as an "undo" of this call. A mute applies when a notification would be written, so unmuting brings back nothing that happened while muted — say so before unmuting if the user expects to catch up, and point them to the record's activity instead. When a user asks why they hear nothing about a record, check `Me_GetRecordMuteStatus` and `Me_GetReasonMutes` before anything else; `Me_GetMutedRecords` lists every muted record they can still read, most recently muted first, with `isFollowing`.

For anything else about yourself, take `employee.id` from `Me_GetProfile` and pass it to the tool's `employeeId` (or role) parameter. If `employee` is null, the account is not linked to an employee, so nothing in Wayd is recorded against the caller as a person: say so rather than substituting the user id. Without the `Me_*` tools (the npm package), use the tools above that default to the caller.

## Resolving a name to an employee id

1. Call `Users_GetUsers` once and filter the result for every person you need. Do not call `Users_GetUser` per person.
2. For each match, take **`employee.id`** — not the user's own `id`. `employee` also carries the person's `name` and integer `key`.
3. If `employee` is null, that user has no employee record and cannot hold a role or be assigned a task. Tell the user rather than substituting the user id.
4. If `isActive` is false, the account is deactivated. Confirm with the user before giving that person a role or a task.
5. If several users match a name, ask which one. Do not pick for them.

To go the other way (employee id in a payload to a name), match it against `employee.id` in the same `Users_GetUsers` result. Some employees have no user account, so an unmatched id is not an error — report it as an id.

If `Users_GetUsers` is denied (it needs the Users view permission), take employee ids from a record the person is already on instead: the role lists on `Projects_GetProject` or the members of `Projects_GetProjectTeam`.
