# Project health checks

## Tools and fields

A health check is a point-in-time assessment of a project — Healthy, At Risk, or Unhealthy — with a reporter, an expiration, and an optional note. Only one non-expired check is active; logging a new one expires the previous one.

| Goal                               | Tool                                |
| ---------------------------------- | ----------------------------------- |
| Health check history, newest first | `Projects_GetProjectHealthChecks`   |
| One health check                   | `Projects_GetProjectHealthCheck`    |
| Log a new assessment               | `Projects_CreateProjectHealthCheck` |
| Correct an existing check          | `Projects_UpdateProjectHealthCheck` |
| Delete a check                     | `Projects_DeleteProjectHealthCheck` |

All five take the project's **UUID**, not its key. The current active check is also embedded as `healthCheck` on `Projects_GetProject` and `Projects_GetProjects`; use that when only the latest status matters.

- **Read and write shapes differ.** A check read back has `status` as an object (`{ id, name }`, with `name` `"At Risk"`); a write takes the string `"Healthy"`, `"AtRisk"`, or `"Unhealthy"`.
- **`expiration` must be in the future** on both create and update (ISO 8601 UTC datetime). An expired check cannot be updated at all — log a new one instead.
- **`note`** is optional, at most 1024 characters.
- All three writes require delivery leadership (see SKILL.md).

## Log, correct, or delete

Default to **logging a new check** whenever the project's health has changed or is being reassessed. History is the point: each check records what was believed at the time.

- **Correct** with `Projects_UpdateProjectHealthCheck` only when the existing check was wrong when it was recorded — a typo in the note, the wrong status picked, a mistaken expiration. The update overwrites every field, so read the check first and resend `status` (as its string value), `expiration`, and `note`.
- **Delete** with `Projects_DeleteProjectHealthCheck` only when the check should never have existed — logged on the wrong project, or a duplicate. Deleting the active check leaves the project with no current health status.

Confirm with the user before any of the three.

## Recommending a health check (checklist)

Copy this checklist and work through it in order:

```text
- [ ] 1. Project and timeline
- [ ] 2. Plan metrics and task progress
- [ ] 3. Critical path
- [ ] 4. Stages
- [ ] 5. Delivery forecast
- [ ] 6. Previous checks and changes since
- [ ] 7. Classify and draft
- [ ] 8. Re-check, then present
```

1. **Project and timeline** — `Projects_GetProject`: status, `start` and `end` (the committed window), current `healthCheck`, and the role lists. The project has no overall progress field; progress comes from steps 2 and 4.
2. **Plan metrics and task progress** — `Projects_GetProjectPlanSummary` (`overdue`, `dueThisWeek`, `upcoming`, `totalLeafTasks`). Then `Tasks_GetProjectTasks` for each task's `status`, `progress`, and planned dates, to name the overdue tasks.
3. **Critical path** — `Tasks_GetCriticalPath` is not implemented yet and always returns an empty list. Treat critical-path evidence as unavailable and say so in the note; never read an empty list as "no critical tasks".
4. **Stages** — if a lifecycle is assigned, `Projects_GetProjectStages`: each stage's status, `progress`, and `start`/`end`. Flag a stage past its end and not complete.
5. **Delivery forecast** — `Projects_GetForecast` (needs the `delivery-forecasting` flag; read it as the `wayd-teams` skill describes). Note `outcome`, `chanceOfFinishingByTargetDate` against the project's `end`, the 50% and 85% dates, and the predecessor with the highest `shareOfTrialsSettingFinish`. `Blocked by Dependency` is itself a signal; any other outcome without dates leaves the forecast out of the classification rather than counting against the project.
6. **Previous checks and changes since** — `Projects_GetProjectHealthChecks`: the latest check's status, `reportedOn`, reporter, and `note`. Then `Projects_GetActivities` for entries after that `reportedOn`: schedule changes (`PreviousDateRange` against the new range), status changes, and role changes. Project activity does **not** include tasks or stages; task progress comes only from step 2. Tasks carry no completion date, so "tasks finished since the last check" can only be inferred by comparing against what the previous note recorded — say so rather than inventing a count.
7. **Classify and draft** using the thresholds below. Draft a `note` (max 1024 characters) covering progress since the last check (or to date, for a first check), schedule and forecast standing, and the specific concerns. Set `expiration` to two weeks from today unless the user gives a review date.
8. **Re-check before presenting**: every overdue or critical-path task you cite exists in the step 2 list; the forecast figures are from a `Forecast` outcome; dates are in the future; the note is within 1024 characters.

### Classification

| Status        | Any of these                                                                                                                                                                                                                     |
| ------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `"Unhealthy"` | Forecast chance below 50% or 85% date well past `end`; outcome `Blocked by Dependency`; a large share of `totalLeafTasks` overdue; the critical path stalled; a stage past its end with substantial work left                    |
| `"AtRisk"`    | Forecast chance 50–80% or 85% date just past `end`; several overdue tasks; a critical-path task overdue or behind its planned dates; a predecessor with a high `shareOfTrialsSettingFinish`; stage progress lagging elapsed time |
| `"Healthy"`   | None of the above: forecast chance above 80% (or no forecast), few or no overdue tasks, critical path on schedule, stages within their windows                                                                                   |

Take the worst status any signal supports, and name the signal that set it.

## Presenting the recommendation

**Never log, correct, or delete a check without the user's confirmation.** Present:

- **Recommended status** — `"Healthy"`, `"AtRisk"`, or `"Unhealthy"`, with the signal that set it
- **Proposed expiration** — UTC datetime
- **Proposed note** — the full text
- **Evidence** — progress since the last check; overdue and critical-path tasks by key; forecast chance and 50%/85% dates against `end`; stage slippage; dependency to chase
- **Authorization** — submitting requires delivery leadership on the project, its program, or its portfolio

Then wait for the user to confirm or adjust before calling `Projects_CreateProjectHealthCheck`.
