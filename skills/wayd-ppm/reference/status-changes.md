# Changing status

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
