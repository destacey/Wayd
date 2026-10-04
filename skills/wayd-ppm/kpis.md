# Strategic initiatives and KPIs

A **strategic initiative** is a portfolio-level outcome — the _why_ behind the work. It belongs to one portfolio, has sponsors and owners, links to the projects delivering it, and measures success through **KPIs**. Each KPI has **checkpoints** (dated targets — the plan) and **measurements** (observed values — the actuals).

Through MCP, initiatives and KPI definitions are read-only: they cannot be created, edited, reordered, or deleted. Initiative status can be changed (see "Changing status" in SKILL.md), and KPI measurements can be added and removed.

## Tools

| Goal                                            | Tool                                           |
| ----------------------------------------------- | ---------------------------------------------- |
| Initiatives, optionally by status or portfolio  | `StrategicInitiatives_GetStrategicInitiatives` |
| Initiatives in one portfolio                    | `Portfolios_GetPortfolioStrategicInitiatives`  |
| One initiative                                  | `StrategicInitiatives_GetStrategicInitiative`  |
| Status values, before filtering by status       | `StrategicInitiatives_GetStatuses`             |
| Projects delivering an initiative               | `StrategicInitiatives_GetProjects`             |
| An initiative's KPIs                            | `StrategicInitiatives_GetKpis`                 |
| One KPI                                         | `StrategicInitiatives_GetKpi`                  |
| Checkpoint definitions only                     | `StrategicInitiatives_GetKpiCheckpoints`       |
| Checkpoints with measurement, health, and trend | `StrategicInitiatives_GetKpiCheckpointPlan`    |
| Measurement history                             | `StrategicInitiatives_GetKpiMeasurements`      |
| Record a measurement                            | `StrategicInitiatives_AddKpiMeasurement`       |
| Remove a measurement                            | `StrategicInitiatives_RemoveKpiMeasurement`    |

For "is this KPI on track?", use `StrategicInitiatives_GetKpiCheckpointPlan` — it is the one call that sets actuals against the plan.

Read tools accept an id or a key for both the initiative and the KPI. The two measurement write tools take **UUIDs only**: the initiative's from `StrategicInitiatives_GetStrategicInitiative`, the KPI's from `StrategicInitiatives_GetKpis`.

## Reading a KPI

- **Check `targetDirection` before describing a trend.** `1=Increase`, `2=Decrease`. For a Decrease KPI (cost, defect count, cycle time) a falling value is improvement.
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
