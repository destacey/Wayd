# PPM Project Date Rollup

## Problem

Roadmaps now keep parent activity dates aligned with child dates: parents expand to contain children, parents cannot be shrunk behind children, and moving a parent without changing duration shifts the whole subtree. Project planning should behave the same way for project phases and project tasks so the PPM plan tree stays coherent.

The important PPM difference is optional planning dates. Project phases and regular tasks can be undated today, and that should remain valid for leaf items and empty containers. A phase or parent task becomes required to have dates only when any of its children have dates.

## Scope

Apply date rollup to:

- `ProjectPhase.DateRange`
- `ProjectTask.PlannedDateRange` for `ProjectTaskType.Task`
- `ProjectTask.PlannedDate` for `ProjectTaskType.Milestone`

Do not change:

- Project-level `Project.DateRange`
- Strategic initiative, program, or portfolio dates
- Finish-to-start dependency scheduling rules
- The existing milestone rule that milestones require a `PlannedDate`

## Domain Rules

### Date Shapes

- Phase dates remain nullable: `ProjectPhase.DateRange` can be `null`.
- Regular task dates remain nullable: `ProjectTask.PlannedDateRange` can be `null`.
- Milestone dates stay required: `ProjectTask.PlannedDate` must be present.
- Ranges are inclusive. A start and end on the same day is valid.

### Rollup Containers

A rollup container is either:

- A project phase containing root tasks.
- A regular project task containing child tasks.

Milestones cannot be containers because milestones cannot have children.

### Effective Child Span

For rollup purposes:

- A dated regular task contributes `[PlannedDateRange.Start, PlannedDateRange.End]`.
- A milestone contributes `[PlannedDate, PlannedDate]`.
- An undated regular task contributes nothing unless it has dated descendants and is therefore rolled up first.
- A child task whose own dates rolled up from descendants contributes its resulting date range.

### Parent Date Requirement

- If a phase or parent task has no dated children, its dates may remain `null`.
- If a phase or parent task has at least one dated child, it must have a non-null date range.
- When a dated child is created, updated, or moved into an undated phase or parent task, that parent receives a date range equal to the dated child span, then ancestors roll up as needed.
- If multiple children have dates, the parent range must cover the minimum child start through the maximum child end.

### Auto-Expansion

- Creating a dated root task outside its phase date range expands the phase.
- Creating a dated child task outside its parent task date range expands the parent task and then the owning phase or ancestor tasks as needed.
- Updating a regular task date range or milestone planned date outside its parent expands ancestors.
- Moving a dated task or subtree to a new parent expands the new parent and ancestors.
- Auto-expansion only grows a container to include children; it does not shrink a wider manually entered range.

### Blocking Shrinks

- A phase cannot be updated to `null` or to a range that excludes any dated root task.
- A parent task cannot be updated to `null` or to a range that excludes any dated direct child.
- The failure should name the offending child where practical, for example: `The date range must contain all child items. "Design Complete" falls outside the selected range.`
- Leaf regular tasks can still be updated to `null`.

### Moving Whole Subtrees

When a regular task with children is updated from one non-null range to another range with the same duration and both endpoints move by the same non-zero number of days, treat it as a move:

- Shift that task and every dated descendant by the same day delta.
- Preserve null dates on undated descendant regular tasks that have no dated descendants.
- Recalculate ancestors after the shift.

When the range duration changes, treat the update as a resize:

- Do not move children.
- Reject the update if the proposed range does not contain all dated direct children.

When the task parent changes in the same operation, treat date changes as a resize, not a subtree move.

### Clearing Dates

Allowed:

- Clear dates on a leaf regular task.
- Clear dates on a phase with no dated root tasks.
- Clear dates on a parent regular task with no dated children.

Rejected:

- Clear dates on a phase with any dated root task.
- Clear dates on a parent regular task with any dated child.
- Clear milestone `PlannedDate`.

When a leaf task is cleared and its parent has no remaining dated children, do not automatically clear the parent. Rollup is grow-only to preserve manually entered dates. Users can clear the parent explicitly when no dated children require it.

## Implementation Notes

### Domain

Add date rollup behavior near the aggregate that owns the hierarchy:

- `Project` should coordinate phase/task lookup and ancestor recalculation because phases and tasks live in the same aggregate.
- `ProjectTask` should expose small internal helpers for:
  - effective date span
  - shifting task/subtree dates
  - applying planned dates with resize vs move semantics
  - linking/reconciling parent navigation if needed for in-memory tests
- `ProjectPhase` should expose an internal date update method that validates child containment through data supplied by `Project`, or a domain method on `Project` should perform the phase update.

Commands that load the aggregate must include enough hierarchy for rollup:

- `CreateProjectTaskCommand` already loads `Project` with `Phases` and `Tasks`.
- `UpdateProjectTaskPlacementCommand` already loads `Project` with `Phases` and `Tasks`.
- `UpdateProjectTaskCommand` currently loads the task alone for most updates and loads `Project` only for parent changes. It should load the project with phases and tasks for date-sensitive updates so ancestor rollup happens consistently.
- `UpdateProjectPhaseCommand` currently loads only the phase. It should load the project with phases and tasks or delegate to a query/update path that has the project aggregate.
- JSON patch endpoints for phases/tasks should use the same command/domain path as full update forms.

### Migration and Backfill

Add a migration that backfills existing PPM plan data so persisted containers satisfy the new rule:

- For each parent task, compute the recursive dated descendant span bottom-up.
- Set `PlannedStart` and `PlannedEnd` when the parent task is missing dates but has dated descendants.
- Expand parent task dates when any dated descendant falls outside the current range.
- For each phase, compute the span of root tasks after task rollup.
- Set or expand phase dates when dated root tasks fall outside the current phase range.
- Preserve wider existing phase/task ranges.
- Do not add dates to undated leaf regular tasks.

No schema change is expected unless existing database constraints currently prevent same-day ranges.

### Frontend

Update the project plan UI to mirror roadmap hints and validation:

- Full create/edit task forms should show an informational hint when selected child dates fall outside the selected parent phase/task and saving will expand that parent.
- Full edit phase/task forms and inline grid edits should block shrinking a phase or parent task behind dated children before the API call.
- Inline phase/task date clearing should be blocked when dated children exist.
- Same-day ranges should be accepted in forms and inline cells.
- If a parent task range is moved by the same duration, communicate that descendants will move with it where the interaction makes that clear.

Relevant files:

- `Wayd.Web/src/wayd.web.reactclient/src/app/ppm/projects/_components/create-project-task-form.tsx`
- `Wayd.Web/src/wayd.web.reactclient/src/app/ppm/projects/_components/edit-project-task-form.tsx`
- `Wayd.Web/src/wayd.web.reactclient/src/app/ppm/projects/_components/edit-project-phase-form.tsx`
- `Wayd.Web/src/wayd.web.reactclient/src/app/ppm/projects/_components/project-plan-table.tsx`
- `Wayd.Web/src/wayd.web.reactclient/src/app/ppm/projects/_components/project-task-patch.ts`

## Tests

### Domain Tests

Add focused tests for:

- Creating a dated root task expands an undated phase.
- Creating a dated child task expands an undated parent task and phase.
- Creating/updating a milestone outside a parent expands ancestors.
- Updating a child task beyond its parent bubbles up through task ancestors and phase.
- Updating a phase to exclude a dated root task fails.
- Updating a parent task to exclude a dated child fails.
- Clearing a leaf regular task date succeeds.
- Clearing a phase or parent task with dated children fails.
- Moving a parent task by the same duration shifts dated descendants.
- Resizing a parent task keeps children fixed and fails when the range would exclude them.
- Moving a dated task/subtree to a new phase or parent expands the new container.

### Application Tests

Cover command paths:

- `CreateProjectTaskCommandHandler`
- `UpdateProjectTaskCommandHandler`
- `UpdateProjectTaskPlacementCommandHandler`
- `UpdateProjectPhaseCommandHandler`
- Phase/task JSON patch endpoints if they bypass the full command handlers.

### Frontend Tests

Cover:

- Same-day date ranges pass validation.
- Parent expansion hints appear for dated children outside the selected parent.
- Phase/task inline edits reject clearing or shrinking when dated children exist.
- Null dates remain allowed for leaf regular tasks.

## Documentation Updates

After implementation, update:

- `docs/user-guide/ppm/projects.mdx`
- `docs/ai/domain-glossary.mdx`
- `docs/llms-full.txt`

Document that phase and parent task dates roll up from child dates, while leaf regular task dates remain optional.
