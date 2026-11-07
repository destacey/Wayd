# Project deliverables

## Tools and fields

A deliverable is something a project has committed to produce for it to count as done, with **acceptance criteria** it is accepted against, an optional owner and target date, and a status. A project cannot be completed while any deliverable is open (`isOpen`: its status is in the Proposed or Active category).

| Goal                                      | Tool                                   |
| ----------------------------------------- | -------------------------------------- |
| A project's deliverables, in order        | `Projects_GetDeliverables`             |
| One deliverable                           | `Projects_GetDeliverable`              |
| Add a deliverable                         | `Projects_CreateDeliverable`           |
| Change name, criteria, owner, target date | `Projects_UpdateDeliverable`           |
| Statuses it can move to                   | `Projects_GetDeliverableStatusOptions` |
| Start, accept, descope, or reopen it      | `Projects_ChangeDeliverableStatus`     |
| Its status changes, newest first          | `Projects_GetDeliverableStatusHistory` |
| Put them in a new order                   | `Projects_ReorderDeliverables`         |
| Delete one added by mistake               | `Projects_DeleteDeliverable`           |
| Set which plan tasks serve it             | `Projects_SetDeliverableTasks`         |

- **Ids.** The reads take the project's UUID or key; every write takes the project's **UUID** and the deliverable's UUID.
- **Name** must be new to the project: two deliverables of one project never share a name, ignoring case, and descoped ones count.
- **Owner** is an employee id (see SKILL.md); a newly named owner must be an active employee.
- **Description and acceptance criteria** are Markdown, at most 4096 characters each; the name at most 256.
- **Status** follows a company-configurable workflow. The default has Not Started, In Progress, Accepted and Descoped, but a company may rename or add stages, so pick the `statusId` from `Projects_GetDeliverableStatusOptions` rather than by name. An option whose `conditions` include `RequiresReason` needs a `reason` — descoping always does.
- **Revision.** `Projects_GetProject` returns the project's `revision`. Send it as `expectedRevision` on a write: if the project changed since, the call answers 409 and you should re-read before retrying.
- Every write requires delivery leadership (see SKILL.md), and a completed or canceled project's deliverables cannot be changed — `Projects_GetDeliverableStatusOptions` returns no options for one.
- A deliverable id that is not on the project answers 404.

## Linking plan tasks

A deliverable is served by the plan tasks linked to it, and a task can serve several deliverables. Links stay within one project.

- **Set them from either side.** `Projects_SetDeliverableTasks` replaces a deliverable's tasks: a task not named is unlinked, and an empty `taskIds` unlinks them all. `deliverableIds` on `Tasks_CreateProjectTask`, `Tasks_UpdateProjectTask` or `Tasks_PatchProjectTask` replaces a task's deliverables; leaving it out of an update keeps them. Read the current links first (`deliverableIds` on each task, or the tasks of `Tasks_GetProjectTasks` that name the deliverable) and send the whole set that should remain.
- **Progress** on a deliverable is derived when read: the mean progress of the leaf tasks beneath its linked tasks, weighted by planned duration in days, with milestones and undated tasks weighing one day. Linking a summary task counts its subtasks; work linked twice through a task and its ancestor counts once. `progress` is null, not 0, when no task is linked; `linkedTaskCount` says how many are.
- **Permissions.** Linking is plan work: it needs only the permission to update projects, no delivery leadership, and it does not change the project's `revision`, so it takes no `expectedRevision`.
- **Progress is not acceptance.** A deliverable at 100% is still open until it is accepted.

## Accept, descope, or delete

- **Accept** only after the user confirms the deliverable meets its acceptance criteria. Accepting records who accepted it and when.
- **Descope** when a deliverable is dropped from the project's scope. It stays on the project with the reason, which is what the user should usually want.
- **Delete** only a deliverable added by mistake. It is refused once the deliverable has been started or the project is Active, Completed or Canceled; descope it instead.

Confirm with the user before any of them, naming the deliverable and, for a descope, the reason you will record.

## Completing or canceling a project

Before `Projects_Complete`, read `Projects_GetDeliverables`. If any is `isOpen`, completion will be refused: tell the user which ones are open, and ask whether each should be accepted or descoped first.

`Projects_Cancel` descopes every open deliverable, with the reason "The project was canceled.", attributed to the caller; accepted and descoped ones are left as they are. Before canceling, tell the user which deliverables it will descope. Reverting the cancellation leaves them descoped.
