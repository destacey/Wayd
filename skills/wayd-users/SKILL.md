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
| What do I follow? Follow or unfollow this project | `Me_GetFollows`; `Me_Follow` / `Me_Unfollow` (following feature flag) |

### Following

`Me_Follow`, `Me_Unfollow` and `Me_GetFollowStatus` take an `ownerType` — `ppm.project`, `ppm.program` or `planning.planning-interval-objective` — and the record's UUID as `ownerId`, never its key: look the record up first. Following acts only for the caller; there is no way to make someone else a follower, so do not offer one. It grants nothing: a record the caller cannot read answers 404 rather than being followed, and drops out of `Me_GetFollows` once they lose access. Following twice and unfollowing something not followed both succeed without changing anything. Following notifies nobody yet, so do not tell the user they will be alerted.

For anything else about yourself, take `employee.id` from `Me_GetProfile` and pass it to the tool's `employeeId` (or role) parameter. If `employee` is null, the account is not linked to an employee, so nothing in Wayd is recorded against the caller as a person: say so rather than substituting the user id. Without the `Me_*` tools (the npm package), use the tools above that default to the caller.

## Resolving a name to an employee id

1. Call `Users_GetUsers` once and filter the result for every person you need. Do not call `Users_GetUser` per person.
2. For each match, take **`employee.id`** — not the user's own `id`. `employee` also carries the person's `name` and integer `key`.
3. If `employee` is null, that user has no employee record and cannot hold a role or be assigned a task. Tell the user rather than substituting the user id.
4. If `isActive` is false, the account is deactivated. Confirm with the user before giving that person a role or a task.
5. If several users match a name, ask which one. Do not pick for them.

To go the other way (employee id in a payload to a name), match it against `employee.id` in the same `Users_GetUsers` result. Some employees have no user account, so an unmatched id is not an error — report it as an id.

If `Users_GetUsers` is denied (it needs the Users view permission), take employee ids from a record the person is already on instead: the role lists on `Projects_GetProject` or the members of `Projects_GetProjectTeam`.
