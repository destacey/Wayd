---
name: wayd-users
description: Guides agents working with Wayd users via the Wayd MCP server, and resolving a person's name to the employee id that role and assignee fields require. Use when looking up a user, or when a project, program, or portfolio role list (sponsorIds, ownerIds, managerIds, memberIds), a task's assigneeIds, or an activity-history payload needs a person matched to an id.
---

# Wayd Users

## User id versus employee id

A user is a sign-in account; an employee is the person record that holds roles and work. Every role and assignee field in Wayd takes an **employee id**, never a user id:

- Project, program, and portfolio role lists: `sponsorIds`, `ownerIds`, `managerIds`, `memberIds`
- Task `assigneeIds`
- `employeeId` filters on the project tools
- People in an activity-history `payload`

A user `id` in any of these does not identify the person.

## Resolving a name to an employee id

1. Call `Users_GetUsers` once and filter the result for every person you need. Do not call `Users_GetUser` per person.
2. For each match, take **`employee.id`** — not the user's own `id`. `employee` also carries the person's `name` and integer `key`.
3. If `employee` is null, that user has no employee record and cannot hold a role or be assigned a task. Tell the user rather than substituting the user id.
4. If `isActive` is false, the account is deactivated. Confirm with the user before giving that person a role or a task.
5. If several users match a name, ask which one. Do not pick for them.

To go the other way (employee id in a payload to a name), match it against `employee.id` in the same `Users_GetUsers` result. Some employees have no user account, so an unmatched id is not an error — report it as an id.

If `Users_GetUsers` is denied (it needs the Users view permission), take employee ids from a record the person is already on instead: the role lists on `Projects_GetProject` or the members of `Projects_GetProjectTeam`.
