# Changing status and deleting

Status changes only through dedicated transition tools, never through an update. **Confirm every transition with the user first**, naming the record and the transition: others rely on the status, and portfolio, program, and initiative transitions cannot be undone.

| Record    | Tools and allowed from                                                                                                                                                                      |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Portfolio | `Portfolios_Activate` (Proposed), `Portfolios_Close` (Active or OnHold), `Portfolios_Archive` (Closed)                                                                                      |
| Program   | `Programs_Activate` (Proposed), `Programs_Complete` (Active), `Programs_Cancel` (any status not already closed)                                                                             |
| Project   | `Projects_Approve` (Proposed), `Projects_Activate` (Proposed or Approved), `Projects_Complete` (Active), `Projects_Cancel` (any status not already closed), `Projects_RevertStatus` (below) |

Strategic initiative transitions are in the initiatives reference listed in SKILL.md.

Every transition tool takes the record's UUID, not its key. No forward transition records a reason; the status history shows who and when, but not why.

## Prerequisites that cause rejections

Check these first so a transition fails in conversation rather than at the API:

- **Project approve** — a lifecycle is assigned.
- **Project or program activate and complete** — both a start and an end date are set.
- **Program complete, or cancel from Active** — every project in it is completed or canceled (`Programs_GetProgramProjects`).
- **Portfolio archive** — the portfolio is Closed.

## Side effects

- **`Portfolios_Activate` stamps the start date as today and `Portfolios_Close` stamps the end date as today.** Neither can be backdated, so never use them to tidy up a portfolio that really started or ended on another date.

## Moving a project backwards

`Projects_RevertStatus` is the only way back to an earlier status — reopening a completed or canceled project, or returning an active one to Approved. Programs and portfolios cannot be reverted.

1. Read `backwardStatusTargets` from `Projects_GetProject` and offer only those. Do not derive targets: a status keeps its entry requirements in either direction (Approved needs a lifecycle, Active needs both dates), so a project cancelled straight from Proposed may only return to Proposed.
2. If the project's program or portfolio is closed, the revert is rejected — that parent must be reopened first.
3. Agree a `reason` with the user. It is required and stored in the status history as the record of why a decision was reversed, so write something a later reader will understand — never "revert" or "fix".
4. Confirm, then call with `toStatus` and `reason`.

## Deleting a record (hosted)

Deleting is permanent and only for a record that never got going. Anything past Proposed is retired with a transition instead — cancel a project or program, close and archive a portfolio — which keeps its history.

| Tool                | Deletes only when                                                                               |
| ------------------- | ----------------------------------------------------------------------------------------------- |
| `Projects_Delete`   | The project is Proposed and **has never left Proposed**                                         |
| `Programs_Delete`   | The program is Proposed and has no projects — move them out first with `Projects_ChangeProgram` |
| `Portfolios_Delete` | The portfolio is Proposed, so it holds no programs, projects, or initiatives yet                |

A project that was approved or activated and later reverted to Proposed stays undeletable. Deleting a project takes its stages, tasks, health checks, scores, and initiative links with it. Project and program deletes are also rejected while the portfolio is Archived. Each takes the record's UUID and checks the delete permission only, not delivery leadership. Strategic initiatives, lifecycles, and expenditure categories have their own delete rules, in their sections.

Before calling, tell the user what will be lost and offer the transition as the alternative; delete only when they choose it.
