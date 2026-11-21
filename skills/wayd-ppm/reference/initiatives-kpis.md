# Strategic initiatives

A **strategic initiative** is a portfolio-level outcome — the _why_ behind the work. It belongs to one portfolio, has sponsors and owners, links to the projects delivering it, and measures success through **KPIs**, read together as its scorecard.

**An initiative's KPIs are worked with the `Kpis_*` tools — read the `wayd-kpis` skill.** Name the initiative with `subjectType` `ppm.strategic-initiative` and `subjectId` its UUID. Those tools need the `kpis` feature flag: while it is off, an initiative shows no scorecard and the KPI tools fail, so tell the user the feature is turned off rather than retrying.

Everything on an initiative checks permissions only, not delivery leadership. Its KPIs follow the same rule: the Update Strategic Initiatives permission changes them, with no role on the initiative needed.

## Tools

| Goal                                           | Tool                                                                                        |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Initiatives, optionally by status or portfolio | `StrategicInitiatives_GetStrategicInitiatives`                                              |
| Initiatives in one portfolio                   | `Portfolios_GetPortfolioStrategicInitiatives`                                               |
| One initiative                                 | `StrategicInitiatives_GetStrategicInitiative`                                               |
| Status values, before filtering by status      | `StrategicInitiatives_GetStatuses`                                                          |
| Projects delivering an initiative              | `StrategicInitiatives_GetProjects`                                                          |
| Create, update, or delete an initiative        | `StrategicInitiatives_Create`, `StrategicInitiatives_Update`, `StrategicInitiatives_Delete` |
| Set the linked projects                        | `StrategicInitiatives_ManageProjects`                                                       |

Read tools accept an id or a key for the initiative. Every write tool takes its **UUID only**, from `StrategicInitiatives_GetStrategicInitiative`; so do the `Kpis_*` tools.

## Changing an initiative's status

Status changes only through dedicated transition tools, never through an update. **Confirm every transition with the user first**, naming the initiative and the transition: others rely on the status, and initiative transitions cannot be undone. Initiative transitions check permissions only, not delivery leadership.

| Record               | Tools and allowed from                                                                                                                                                                                   |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Strategic initiative | `StrategicInitiatives_Approve` (Proposed), `StrategicInitiatives_Activate` (Approved), `StrategicInitiatives_Complete` (Active or OnHold), `StrategicInitiatives_Cancel` (any status not already closed) |

Every transition tool takes the initiative's UUID, not its key. No transition records a reason; the status history shows who and when, but not why.

**Completing or cancelling a strategic initiative closes it**: its linked projects are frozen. Its KPIs stay open to change, since benefits are often measured after an initiative ends.

## Creating, updating, and deleting an initiative

- **Create** with `StrategicInitiatives_Create`: `name`, `description`, `start` and `end` dates, the `portfolioId` (an active or on-hold portfolio), and optional `sponsorIds` and `ownerIds` (employee ids). It starts in Proposed.
- **Update** with `StrategicInitiatives_Update`, which overwrites the whole record. Read the initiative first and send back `name`, `description`, `start`, `end`, and **every** sponsor and owner: an omitted or empty role list removes everyone in that role. Status, portfolio, KPIs, and projects are not changed by it.
- **Delete** with `StrategicInitiatives_Delete` only while it is Proposed or Approved; its KPIs, with their measurements and checkpoint plans, go with it, and its project links are removed. An Active or closed initiative cannot be deleted — cancel it instead.

## Linking projects

`StrategicInitiatives_ManageProjects` takes the **complete** set of linked project UUIDs: anything left out is unlinked, and an empty list unlinks everything. Read the current links with `StrategicInitiatives_GetProjects`, add or drop only what the user asked for, name any project that would be unlinked, and confirm. Rejected once the initiative is closed.
