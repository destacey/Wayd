---
name: wayd-imports
description: Guides agents importing CSV data into Wayd via the Wayd MCP server — writing a file in the right format, checking it with a preflight, importing the rows it checked, and following, stopping, resuming or retrying import runs. Use when loading records from a spreadsheet or another system, migrating history into Wayd, or finding out what became of an import someone submitted.
---

# Wayd Imports (Preflight / Apply / Runs)

## When to use

- Loading records into Wayd from a spreadsheet or another system — employees, teams, portfolios, programs, projects, tasks, planning intervals, objectives, risks, products, versions, packages, releases, environments, deployments
- Checking whether a file would import cleanly, and why rows would be refused
- Following a run that was still going when it was submitted
- Finding out what became of an import someone submitted from Settings → Imports
- Stopping a run, resuming one that stopped, or retrying rejected rows

For editing a handful of records, use the area's own create and update tools instead: an import creates records, and most imports only create.

---

## A file only gets in through a preflight

There is no tool that imports a file in one step. `Imports_Preflight` sends the file with `validateOnly` forced on: every row goes through the checks a real import runs, in the same order, against the data as it stands, and **nothing is created**. The only way to import is `Imports_Apply` on that finished preflight, which imports exactly the rows it checked without sending the file again.

That split is deliberate. Show the user what the preflight found — how many rows would be imported, and every refusal with its reason — before calling `Imports_Apply`. Apply is advertised as destructive, so the client will ask them to confirm.

A preflight is **advice, not a promise**. Each row is checked again when it is applied, so a record someone created or removed in between can still refuse a row the preflight passed.

---

## The flow

1. **`Imports_GetDefinitions`** — the kinds of import you may see. Use one with `canSubmit: true`. Note its `atomicity` and `preflightMaxRows`.
2. **`Imports_GetFileFormat`** with the `importType` key — every file the import takes and every column: its exact `header`, whether a row must fill it, its type, and the values it accepts.
3. **Write the CSV.** Start from the `header` string. Every column must be present even when no row fills it, or the whole file is refused with `Header with name 'X' was not found`.
4. **`Imports_Preflight`** with `importType`, the CSV text in `file`, and a second file where the format lists one (`manifestFile`, `contentsFile`, `kpiFile`).
5. **Wait for it.** The answer is the run. If `isTerminal` is false, poll `Imports_GetById` until it is true.
6. **`Imports_GetRows`** with `status: Failed` — each refused row's `importId` and `error`. Rows with a `warning` would be imported but record something worth telling the user.
7. **Fix and check again**, or present the result. Fix the file, or the data a row depends on, then run a new preflight. A preflight cannot be resumed or retried.
8. **`Imports_Apply`** with the preflight's `id`, once the user agrees. It answers with a **new** run; follow it as in step 5 and read its rows the same way.

A file refused outright — a missing header, a cell that is the wrong type, too many rows, an empty file — comes back as an HTTP 400 or 422 and **becomes no run**. The error lists each problem by column and names the row, usually by its `ImportId`.

---

## Rules every file follows

- **`ImportId` is yours.** Any value unique within the file (compared case-insensitively). Outcomes are reported against it, and rows refer to each other by it: a KPI names its initiative's `ImportId`, a manifest line its package's. Left blank, a row is known by its position. It is never stored on the record.
- **References are by id or natural key, never by display name.** A project is named by its `Key` and a person by employee number, but portfolios, programs, teams, products and most other records by id. Look ids up with the area's read tools before writing the file. The column descriptions from `Imports_GetFileFormat` say which a column takes.
- **Lists are semicolon-separated** (`E-1001;E-1002`), because a comma is the field delimiter.
- **Dates are `yyyy-MM-dd`.** A date column refuses a timestamp rather than truncating it. Timestamp columns say so in the format.
- **Imports create through the normal domain rules**, as if the records were entered in the app — so a program still needs an active portfolio, and a released release still needs its contents shipped.

---

## All-or-nothing versus row by row

`atomicity` decides what a rejected row costs:

- **Atomic** — one rejected row means **nothing is written**. A run can report "0 of 40 applied" and be working correctly. Every import except employees is atomic. A preflight of an atomic import still reports every rejection at once, rather than stopping at the first step that finds one, and says Failed while any row is refused.
- **PerRow** — each row stands alone, so a run can be PartiallySucceeded. Only employees import this way.

For an atomic import, get the preflight to zero rejections before applying.

---

## Several files depend on each other

Records reference each other, so files go in dependency order, and **each must be applied before the next is written**: a preflight creates nothing, so a program's preflight cannot see a portfolio that has only been checked. Read `createdEntityId` from the applied run's rows to fill the next file's id columns.

- **Organization**: employees → teams → team hierarchy (`team-memberships`) → team staffing (`team-members`). Team member roles must exist first.
- **Planning**: teams → planning intervals → objectives and risks.
- **PPM**: expenditure categories and lifecycles (in Settings, not imported) → strategic themes → portfolios → programs → projects → project tasks → project stage statuses → strategic initiatives with KPIs → finalizations. Programs and portfolios that should end up closed are imported active, and `ppm.finalizations` closes them once their contents are in.
- **Product Management**: product types and tags (in Settings) → products → versions → release packages → releases. Environments → deployments, after versions and packages.

To show files as one batch in Settings → Imports, pass the same `submissionGroupId` (a GUID you choose) on each preflight and apply. It is a label only and changes nothing about ordering.

---

## Limits

- **Rows per file**: `maxRows`, and `preflightMaxRows` for a preflight (10,000 for every import today, including employees, whose real imports take up to 50,000). Split a larger file, and group the parts with a `submissionGroupId`.
- **Row data is kept for 30 days** after a run finishes. After that a preflight can no longer be applied and a run can no longer be resumed or retried; submit the file again.
- **Row and run listings return at most 500 per page.** Page through `Imports_GetRows` before telling the user every row succeeded.

---

## Applying twice duplicates

`Imports_Apply` stays available after a preflight has been applied, because the import may have failed for a reason since fixed. Check the preflight's `appliedImportProcessId` first. If it is set, the rows went in once already: applying again refuses them as duplicates where the import has a natural key, and **creates them a second time** where it does not, as with deployments.

---

## Acting on a run

Each is refused on a run you may not submit, which `canManage` reports.

| Situation | Tool | What happens |
|---|---|---|
| A run is going and should not be | `Imports_Cancel` | Stops at the next batch boundary. Rows already applied **stay applied**; this is not an undo. |
| A run stopped or failed partway | `Imports_Resume` | Applies the rows it never reached. Rows that succeeded are never reapplied. |
| Rows were rejected for something since fixed | `Imports_RetryFailed` | Reattempts rejected rows as well as unreached ones, with the data as submitted, so a problem in the file itself needs a new file. |
| A preflight is ready | `Imports_Apply` | Imports its rows as a new run. |

Resume and retry answer with `queuedRowCount` and `skippedRowCount`; skipped rows lost their data to the retention window. Neither works on a preflight or on a run still going.

---

## Finding an existing import

- **Recent runs**: `Imports_GetList`, newest first. Filter by `status`, `importType` (the key), `submittedByUserId`, or `submissionGroupId` for a batch.
- **Why a run failed**: `Imports_GetById` for `error`, which explains a run that could not continue, then `Imports_GetRows` with `status: Failed` for the rows refused.

You see runs of the kinds you may submit. Holding View Imports shows every kind, read-only.
