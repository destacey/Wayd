# Strategic initiatives and KPIs

A **strategic initiative** is a portfolio-level outcome — the _why_ behind the work. It belongs to one portfolio, has sponsors and owners, links to the projects delivering it, and measures success through **KPIs**. Each KPI has **checkpoints** (dated targets — the plan) and **measurements** (observed values — the actuals).

Initiative status can be changed and KPI measurements added and removed with any Wayd MCP server. Creating, updating, and deleting initiatives and KPIs, setting checkpoint plans, and linking projects are offered only by the hosted endpoint (`/mcp`). Everything on an initiative checks permissions only, not delivery leadership.

## Tools

| Goal                                             | Tool                                                                                                                                     |
| ------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Initiatives, optionally by status or portfolio   | `StrategicInitiatives_GetStrategicInitiatives`                                                                                           |
| Initiatives in one portfolio                     | `Portfolios_GetPortfolioStrategicInitiatives`                                                                                            |
| One initiative                                   | `StrategicInitiatives_GetStrategicInitiative`                                                                                            |
| Status values, before filtering by status        | `StrategicInitiatives_GetStatuses`                                                                                                       |
| Projects delivering an initiative                | `StrategicInitiatives_GetProjects`                                                                                                       |
| An initiative's KPIs                             | `StrategicInitiatives_GetKpis`                                                                                                           |
| One KPI                                          | `StrategicInitiatives_GetKpi`                                                                                                            |
| Checkpoint definitions only                      | `StrategicInitiatives_GetKpiCheckpoints`                                                                                                 |
| Checkpoints with measurement, health, and trend  | `StrategicInitiatives_GetKpiCheckpointPlan`                                                                                              |
| Measurement history                              | `StrategicInitiatives_GetKpiMeasurements`                                                                                                |
| Record a measurement                             | `StrategicInitiatives_AddKpiMeasurement`                                                                                                 |
| Remove a measurement                             | `StrategicInitiatives_RemoveKpiMeasurement`                                                                                              |
| Create, update, or delete an initiative (hosted) | `StrategicInitiatives_Create`, `StrategicInitiatives_Update`, `StrategicInitiatives_Delete`                                              |
| Set the linked projects (hosted)                 | `StrategicInitiatives_ManageProjects`                                                                                                    |
| Create, update, delete, or reorder KPIs (hosted) | `StrategicInitiatives_CreateKpi`, `StrategicInitiatives_UpdateKpi`, `StrategicInitiatives_DeleteKpi`, `StrategicInitiatives_ReorderKpis` |
| Set a KPI's checkpoint plan (hosted)             | `StrategicInitiatives_ManageKpiCheckpointPlan`                                                                                           |

For "is this KPI on track?", use `StrategicInitiatives_GetKpiCheckpointPlan` — it is the one call that sets actuals against the plan.

Read tools accept an id or a key for both the initiative and the KPI. Every write tool takes **UUIDs only**: the initiative's from `StrategicInitiatives_GetStrategicInitiative`, the KPI's from `StrategicInitiatives_GetKpis`.

## Changing an initiative's status

Status changes only through dedicated transition tools, never through an update. **Confirm every transition with the user first**, naming the initiative and the transition: others rely on the status, and initiative transitions cannot be undone. Initiative transitions check permissions only, not delivery leadership.

| Record               | Tools and allowed from                                                                                                                                                                                   |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Strategic initiative | `StrategicInitiatives_Approve` (Proposed), `StrategicInitiatives_Activate` (Approved), `StrategicInitiatives_Complete` (Active or OnHold), `StrategicInitiatives_Cancel` (any status not already closed) |

Every transition tool takes the initiative's UUID, not its key. No transition records a reason; the status history shows who and when, but not why.

**Completing or cancelling a strategic initiative closes it**: its KPIs and linked projects are frozen, though measurements can still be recorded.

## Reading a KPI

- **Check `targetDirection` before describing a trend.** It is `Increase` or `Decrease`. For a Decrease KPI (cost, defect count, cycle time) a falling value is improvement.
- **Report the supplied `progress`** rather than recomputing it from `startingValue`, `targetValue`, `actualValue`, and direction.
- **`actualValue` is the measurement with the latest measurement date**, not the most recently entered one. A back-dated measurement does not change it.
- **Include `prefix` and `suffix`** (`$`, `%`, `M`) when quoting a value.
- **A checkpoint with null `measurement`, `health`, and `trend` has not been measured yet.** Report it as "not measured", never as failing.
- **A closed initiative is frozen.** Once an initiative is completed or cancelled, its KPIs and linked projects can no longer change, but measurements can still be recorded.

## Recording a measurement

1. Resolve the initiative UUID with `StrategicInitiatives_GetStrategicInitiative` and the KPI UUID with `StrategicInitiatives_GetKpis`.
2. Check `StrategicInitiatives_GetKpiMeasurements` for an existing measurement on the same date. Dates are unique within a KPI, so a duplicate is rejected rather than treated as an update.
3. Call `StrategicInitiatives_AddKpiMeasurement`. The body's `strategicInitiativeId` and `kpiId` must match the path parameters. `actualValue` must be non-zero; `measurementDate` is an ISO 8601 UTC datetime; `note` is optional (max 1024 characters).
4. Re-read the KPI and report the new `actualValue` and `progress` — remembering they change only if this measurement has the latest date.

Measurements are history. To record a new observation, always add one; never delete the previous one. Use `StrategicInitiatives_RemoveKpiMeasurement` only to correct a wrong entry — for example, to free a date so its value can be re-recorded — and confirm with the user first, because it rewrites the record of what was known when.

## Creating, updating, and deleting an initiative (hosted)

- **Create** with `StrategicInitiatives_Create`: `name`, `description`, `start` and `end` dates, the `portfolioId` (an active or on-hold portfolio), and optional `sponsorIds` and `ownerIds` (employee ids). It starts in Proposed.
- **Update** with `StrategicInitiatives_Update`, which overwrites the whole record. Read the initiative first and send back `name`, `description`, `start`, `end`, and **every** sponsor and owner: an omitted or empty role list removes everyone in that role. Status, portfolio, KPIs, and projects are not changed by it.
- **Delete** with `StrategicInitiatives_Delete` only while it is Proposed or Approved; its KPIs, checkpoints, and measurements go with it, and its project links are removed. An Active or closed initiative cannot be deleted — cancel it instead.

## Linking projects (hosted)

`StrategicInitiatives_ManageProjects` takes the **complete** set of linked project UUIDs: anything left out is unlinked, and an empty list unlinks everything. Read the current links with `StrategicInitiatives_GetProjects`, add or drop only what the user asked for, name any project that would be unlinked, and confirm. Rejected once the initiative is closed.

## Managing KPIs (hosted)

KPI definitions cannot change once the initiative is completed or canceled.

- **Create** with `StrategicInitiatives_CreateKpi`: `name`, a non-zero `targetValue`, `targetDirection` (`Increase` or `Decrease`), and optionally `description`, `startingValue`, and a display `prefix`/`suffix` (at most 8 characters). A `startingValue` must be below the target for Increase and above it for Decrease. The body's `strategicInitiativeId` must match the path.
- **Update** with `StrategicInitiatives_UpdateKpi`, which overwrites the whole definition: read the KPI first and send back every field, or an omitted description, starting value, prefix, or suffix is cleared. It does not touch measurements or checkpoints.
- **Delete** with `StrategicInitiatives_DeleteKpi` — it takes every checkpoint and measurement of the KPI with it, so name how many measurements will be lost.
- **Reorder** with `StrategicInitiatives_ReorderKpis`: `orderedKpiIds` lists every KPI exactly once, in the new order.

### Setting a checkpoint plan

`StrategicInitiatives_ManageKpiCheckpointPlan` replaces the KPI's whole plan.

1. Read the current checkpoints with `StrategicInitiatives_GetKpiCheckpoints`.
2. Build `checkpoints`: every checkpoint to keep with its `checkpointId` (edited or not), new ones without an id. Anything left out is deleted.
3. Each checkpoint needs a `targetValue`, a `checkpointDate` (ISO 8601 UTC datetime, unique within the KPI), and a `dateLabel` of at most 16 characters (`Q1 2027`); `atRiskValue` is an optional threshold that must differ from the target.
4. Show the user the plan, naming any checkpoint that would be removed, and confirm. The body's `strategicInitiativeId` and `kpiId` must match the path.
