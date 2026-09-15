---
"@wayd/mcp": minor
---

Add CSV import tools: check a file, import the rows it checked, and follow or re-run import runs.

**Files.** `Imports_GetFileFormat` describes the columns each kind of import takes, answered from the API description the package was built against without a request. `Imports_Preflight` sends CSV text to any of the 21 import endpoints with `validateOnly` forced on, so every row is checked against the data as it stands and nothing is created.

**Runs.** `Imports_GetDefinitions`, `Imports_GetList`, `Imports_GetById` and `Imports_GetRows` follow a run and give each refused row's reason, whether the file was sent by the server or submitted from Settings → Imports. `Imports_Cancel`, `Imports_Resume`, `Imports_RetryFailed` and `Imports_Apply` act on one.

There is deliberately no tool that imports a file in one step. Rows reach an import only through `Imports_Apply` on a finished preflight, which is annotated `destructiveHint`, so an agent has seen every row's outcome before the client asks to confirm. Three behaviours worth knowing when approving an apply or re-run:

- **A preflight is advice, not a promise.** Each row is checked again when applied, so a row the preflight passed can still be refused.
- **Applying the same preflight twice duplicates** the records of an import with no natural key, such as deployments. `appliedImportProcessId` on the preflight says whether it has been applied.
- **Cancelling is not an undo.** Rows already applied stay applied.

Ships with a new agent skill, `wayd-imports`, covering the preflight-then-apply flow, the order files that depend on each other must be applied in, and the row and retention limits.

The run tools need Wayd 0.207.0 or later, and `Imports_Preflight` and `Imports_Apply` need 0.210.0. Wayd 0.207.0 to 0.209.0 ignore `validateOnly` and would import a preflight for real, so before posting `Imports_Preflight` reads the import definitions and sends nothing to an instance whose definitions lack `preflightMaxRows`.
