---
'@wayd/mcp': minor
---

Add strategic initiative and KPI tools, closing the largest remaining gap in PPM coverage.

Strategic initiatives are the portfolio-level outcomes the organisation is pursuing, with KPIs as the measures of success and projects as the delivery vehicles. None of that surface was previously reachable from MCP.

Reads: `StrategicInitiatives_GetStrategicInitiatives`, `_GetStrategicInitiative`, `_GetStatuses`, `_GetProjects`, `_GetKpis`, `_GetKpi`, `_GetKpiCheckpoints`, `_GetKpiCheckpointPlan`, `_GetKpiMeasurements`, plus `Portfolios_GetPortfolioStrategicInitiatives` for the portfolio-scoped list.

Writes: `StrategicInitiatives_AddKpiMeasurement` and `_RemoveKpiMeasurement`. Recording a KPI measurement is periodic, low-blast-radius reporting work, so it follows the precedent set by project health checks. Everything else stays read-only — initiatives cannot be created, updated, or deleted, and KPIs cannot be added, edited, reordered, or deleted. (Initiative *status* transitions land separately in this same release.)

Note the ID asymmetry: the read tools accept an ID **or** a key for both the initiative and the KPI, while the two measurement write tools take UUIDs only and cross-check the body against the path.

Three domain behaviours are documented in the `wayd-ppm` skill because they are easy to get backwards. A KPI's `targetDirection` may be Decrease, where a falling value is improvement, so lower is not universally worse. A KPI's headline `actualValue` is the measurement with the latest measurement *date*, not the most recently entered one, so back-dating does not change it. And measurement dates must be unique within a KPI, so re-submitting an existing date is rejected rather than treated as an update.
