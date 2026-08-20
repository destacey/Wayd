---
'@wayd/mcp': minor
---

**Breaking:** rename the PPM "phase" concept to "stage", renaming two tools and their parameters. Released as a minor bump because the package is still pre-1.0.

`Projects_GetProjectPhases` becomes `Projects_GetProjectStages`, and `Projects_GetProjectPhase` becomes `Projects_GetProjectStage`. The latter's `phaseId` parameter is now `stageId`. Both are read-only GETs and their behaviour is unchanged — only the names move.

This is breaking for any agent, prompt, or saved workflow that calls those tools by name or passes `phaseId`. There is no alias for the old names: a call to `Projects_GetProjectPhases` now fails as an unknown tool rather than silently returning stale data, which is the safer failure. Callers should switch to the new names; nothing else about the request or response shape changes.

The rename runs through the whole product, not just this package. The underlying API routes moved from `/api/ppm/projects/{id}/phases` to `/stages` (and `/api/ppm/project-lifecycles/{id}/phases` to `/stages`), so an older `@wayd/mcp` pointed at a current Wayd API will 404 on those two tools — upgrading this package and the API together is the supported path.

`Projects_GetProjectPlanTree` keeps its name, but its description now says the top-level nodes are stages rather than phases, matching what the endpoint actually returns.

Two related concepts were distinct before the rename and remain distinct after it, which is worth keeping straight when reading the plan tree: a **project lifecycle stage** is the template definition on a lifecycle, and a **project stage** is the per-project instance copied from it when a lifecycle is assigned. Both previously used the word "phase". The `wayd-ppm` skill is updated to match.
