# @wayd/mcp

## 0.13.0

### Minor Changes

- 21dd838: The four forecast tools accept an optional `startedWorkFirst` (default true), which counts active backlog work items ahead of proposed ones, each in rank order, since teams usually finish what they have started. Pass false to order backlogs by rank alone.

## 0.12.0

### Minor Changes

- 3bbdbd5: Forecast delivery and grade backlog health.

  - `Workspaces_GetWorkItemForecast`, `PlanningIntervals_GetObjectiveForecast` and `Projects_GetForecast` forecast when a work item, PI objective or project will be done, with the chance of finishing by a target date. Each accepts optional `targetDate`, `lookbackDays` and `ignoreDependencies`.
  - `Teams_GetThroughputForecast` forecasts how many backlog work items a team finishes by a `targetDate`, and which work item that reaches.
  - `Teams_GetBacklogHealth` grades a team's open backlog and lists the work items each check flagged, with optional what-if thresholds.

  The forecasts require the `delivery-forecasting` feature flag and return 404 when it is off.

## 0.11.0

### Minor Changes

- 7c639f5: Record how a product reaches one it depends on, and replace `Products_ChangeDependencyStrength` with `Products_ChangeDependencyTerms`.

  - `Products_GetDependencies` reports each dependency's `interactionStyles` (`Synchronous`, `Asynchronous`, or both).
  - `Products_AddDependency` accepts optional `interactionStyles`.
  - `Products_UpdateDependency` accepts `interactionStyles` to record them in place on a dependency where none were recorded.
  - `Products_ChangeDependencyTerms` replaces `Products_ChangeDependencyStrength` on `/api/product-management/products/{id}/dependencies/{dependencyId}/terms`, allowing changes to strength, interaction styles, or both.

## 0.10.0

### Minor Changes

- 17ca15a: Add `Deployments_Delete`, which permanently deletes a deployment and its status history. It needs the new delivery Delete permission. Delivery measures stop counting a deleted deployment, so a real failure or rollback should still be recorded as one.
- bda3950: Add `DeploymentEnvironments_Delete`, which permanently deletes an environment and every deployment into it, with their status history. It needs the new environment Delete permission, plus the delivery Delete permission when the environment has deployments. Retiring an environment is still the way to take it out of use while keeping its deployments.
- f2c6e71: Add `ReleasePackages_Delete`, which permanently deletes a package with its manifest, status history and every deployment of it. It is refused while any release lists the package. It needs the delivery Delete permission. Withdrawing is still the way to pull a package while keeping its history.
- d29d732: Add `Versions_Delete`, which permanently deletes a version with its status history and every deployment of it. It is refused while a release lists the version or a package manifest names it, and needs the delivery Delete permission.
- 31d4833: Add `Releases_Delete`, which permanently deletes a release with its contents list and status history. The versions and packages it listed are kept. It needs the new release Delete permission.
- b6f827f: Add `ProductTagCategories_DeleteTag`, which permanently deletes a tag no product carries. A tag in use, or one on a seeded system category, is refused; deactivate it instead.

### Patch Changes

- d47a0fb: `Products_Delete` now also removes the product's status history, which was left behind before. The tool and skill describe how to purge a retired product from the bottom up.

## 0.9.0

### Minor Changes

- 2c79b1e: Add product dependency, rollout and delivery overview tools, answering "what relies on this?", "what is running where?" and "how often are we shipping?".

  **Product dependencies** — `Products_GetDependencies`, `Products_AddDependency`, `Products_UpdateDependency`, `Products_ChangeDependencyStrength`, `Products_EndDependency` and `Products_RemoveDependency`. Reading is rolled up across everything beneath the product, so a product line's list includes its services' links to anything outside it, and a link inside the line appears in neither list. Strength (Hard or Soft) has no default. A dependency that stopped should be ended, which keeps it; remove is for one that was never true. Changing strength ends the dependency and opens a new one, and returns the new id.

  **What is running where** — `DeploymentEnvironments_GetRollout` lists each environment in ring order with the latest deployment of each product that succeeded and was not rolled back, packages expanded into their components, and `hasFailedAttemptSince` where a later attempt failed.

  **Version activity** — `DeliveryOverview_GetDeliveryOverview` measures release frequency and cut-to-released time from versions rather than deployments, for a product and everything beneath it; `DeliveryOverview_GetRecentDeliveryEvents` is a feed of the latest status change of each version and package.

  Also:

  - The environment category accepts **5 Other**, for a sandbox or demo target that is never counted as production.
  - Activity entries can be **related**: `isRelated` and `raisedOn` mark an entry raised on another record, such as a child product moving in or out of this one.
  - Import definitions and runs can have **PerGroup** atomicity, where rows sharing a group apply together and other groups are kept. The product dependency import applies product by product.
  - `Products_Reparent` and `Products_Delete` describe the refusals dependencies add.

  The `wayd-products`, `wayd-delivery` and `wayd-imports` skills cover the new tools. The rollout, delivery overview and related-activity tools need Wayd 0.211.0 or later; the dependency tools and the PerGroup import need 0.212.0.

## 0.8.0

### Minor Changes

- d9f43f9: Add CSV import tools: check a file, import the rows it checked, and follow or re-run import runs.

  **Files.** `Imports_GetFileFormat` describes the columns each kind of import takes, answered from the API description the package was built against without a request. `Imports_Preflight` sends CSV text to any of the 21 import endpoints with `validateOnly` forced on, so every row is checked against the data as it stands and nothing is created.

  **Runs.** `Imports_GetDefinitions`, `Imports_GetList`, `Imports_GetById` and `Imports_GetRows` follow a run and give each refused row's reason, whether the file was sent by the server or submitted from Settings → Imports. `Imports_Cancel`, `Imports_Resume`, `Imports_RetryFailed` and `Imports_Apply` act on one.

  There is deliberately no tool that imports a file in one step. Rows reach an import only through `Imports_Apply` on a finished preflight, which is annotated `destructiveHint`, so an agent has seen every row's outcome before the client asks to confirm. Three behaviours worth knowing when approving an apply or re-run:

  - **A preflight is advice, not a promise.** Each row is checked again when applied, so a row the preflight passed can still be refused.
  - **Applying the same preflight twice duplicates** the records of an import with no natural key, such as deployments. `appliedImportProcessId` on the preflight says whether it has been applied.
  - **Cancelling is not an undo.** Rows already applied stay applied.

  Ships with a new agent skill, `wayd-imports`, covering the preflight-then-apply flow, the order files that depend on each other must be applied in, and the row and retention limits.

  The run tools need Wayd 0.207.0 or later, and `Imports_Preflight` and `Imports_Apply` need 0.210.0. Wayd 0.207.0 to 0.209.0 ignore `validateOnly` and would import a preflight for real, so before posting `Imports_Preflight` reads the import definitions and sends nothing to an instance whose definitions lack `preflightMaxRows`.

- d9f43f9: Add activity history tools, answering "what changed on this, and who changed it?" for the records the server already reaches.

  `Portfolios_GetActivities`, `Programs_GetActivities`, `Projects_GetActivities`, `StrategicInitiatives_GetActivities`, `PlanningIntervals_GetActivities`, `PlanningIntervals_GetObjectiveActivities`, `Teams_GetActivities`, `Products_GetActivities`, `Releases_GetActivities`, `Versions_GetActivities`, `ReleasePackages_GetActivities` and `Deployments_GetActivities`. All are read-only, take an ID or key, and return entries newest first, at most 100 per page.

  Each entry has a category (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), who acted and when, a one-line summary, and a `payload` holding the event's fields as a JSON string. Three things worth knowing when reading one:

  - **A change carries both ends.** The payload holds the value before and after, so one entry answers what moved.
  - **People in a payload are employee ids**, not the user ids `Users_GetUsers` returns.
  - **A Baseline entry is where tracking began** for a record that existed before its changes were recorded. Nothing earlier is available, so it is not the record's creation.

  Each record's history is its own. A project's includes its health checks and scores but not its tasks or stages, and a program's does not include its projects.

  The `wayd-ppm`, `wayd-pi`, `wayd-teams`, `wayd-products` and `wayd-delivery` skills explain how to read them. Every tool needs Wayd 0.210.0 or later. Most endpoints arrived in 0.207.0, but the planning interval and objective histories did not, and an older instance answers those with 404.

### Patch Changes

- f78a3e7: Show the reason in an API error. Error bodies were cut at 200 characters of raw JSON, and the API puts a boilerplate title first, so a validation failure arrived as `See the erro...` and a refusal from a domain rule lost its message. A problem details response is now reported as its title, its detail and each validation error by field.

## 0.7.0

### Minor Changes

- 1cb65ba: Add product management tools, covering the module end to end: the product catalog, the type and tag configuration it is built from, and the delivery records that track what shipped.

  **Catalog.** `Products_GetProducts`, `_GetProduct`, `_GetStatusHistory`, `_GetStatusOptions`, `_Create`, `_Update`, `_Retype`, `_Reparent`, `_ChangeStatus`, `_LinkExternally`, `_Tag`, `_Untag`, `_Delete`. The catalog is one typed tree, and a product's type carries the flag deciding whether versions can be cut against it. Type, parent and status are separate tools rather than fields on the update, so a refusal says which rule refused.

  **Configuration.** `ProductTypes_*` and `ProductTagCategories_*` — create, update, activate, delete, reorder, plus the tags on an axis. Two rules run through all of it: seeded system records cannot be modified or deleted but _can_ be deactivated, and nothing in use can be deleted. Tags inside a system axis are the exception, refusing deactivation too, so there is no per-tag fallback.

  **Delivery.** `Releases_*`, `Versions_*`, `ReleasePackages_*`, `Deployments_*`, `DeploymentEnvironments_*` and `DeliveryMetrics_GetDeliveryMetrics`. The four record types are kept apart deliberately: a release is what was announced to customers, a version is one artifact that was built, a package is what moved through environments together, and a deployment is one of those reaching one environment.

  Writes are annotated `destructiveHint`, so clients confirm before running them. Three behaviours worth knowing when approving one:

  - **Contents and manifests replace as a set.** `Releases_SetContents` and `ReleasePackages_SetManifest` overwrite what was there, so send the full list including anything that should stay.
  - **Tagging a single-value axis silently replaces** the existing tag rather than refusing.
  - **`ProductTypes_Update` requires `isReleasable`.** It is a whole-record overwrite, so a rename that resends the wrong value silently changes whether versions can be cut against every product of that type.

  Ships with two agent skills, `wayd-products` and `wayd-delivery`.

## 0.6.0

### Minor Changes

- 7908808: **Breaking:** rename the PPM "phase" concept to "stage", renaming two tools and their parameters. Released as a minor bump because the package is still pre-1.0.

  `Projects_GetProjectPhases` becomes `Projects_GetProjectStages`, and `Projects_GetProjectPhase` becomes `Projects_GetProjectStage`. The latter's `phaseId` parameter is now `stageId`. Both are read-only GETs and their behaviour is unchanged — only the names move.

  This is breaking for any agent, prompt, or saved workflow that calls those tools by name or passes `phaseId`. There is no alias for the old names: a call to `Projects_GetProjectPhases` now fails as an unknown tool rather than silently returning stale data, which is the safer failure. Callers should switch to the new names; nothing else about the request or response shape changes.

  The rename runs through the whole product, not just this package. The underlying API routes moved from `/api/ppm/projects/{id}/phases` to `/stages` (and `/api/ppm/project-lifecycles/{id}/phases` to `/stages`), so an older `@wayd/mcp` pointed at a current Wayd API will 404 on those two tools — upgrading this package and the API together is the supported path.

  `Projects_GetProjectPlanTree` keeps its name, but its description now says the top-level nodes are stages rather than phases, matching what the endpoint actually returns.

  Two related concepts were distinct before the rename and remain distinct after it, which is worth keeping straight when reading the plan tree: a **project lifecycle stage** is the template definition on a lifecycle, and a **project stage** is the per-project instance copied from it when a lifecycle is assigned. Both previously used the word "phase". The `wayd-ppm` skill is updated to match.

## 0.5.0

### Minor Changes

- 5cf527a: Add strategic initiative and KPI tools, closing the largest remaining gap in PPM coverage.

  Strategic initiatives are the portfolio-level outcomes the organisation is pursuing, with KPIs as the measures of success and projects as the delivery vehicles. None of that surface was previously reachable from MCP.

  Reads: `StrategicInitiatives_GetStrategicInitiatives`, `_GetStrategicInitiative`, `_GetStatuses`, `_GetProjects`, `_GetKpis`, `_GetKpi`, `_GetKpiCheckpoints`, `_GetKpiCheckpointPlan`, `_GetKpiMeasurements`, plus `Portfolios_GetPortfolioStrategicInitiatives` for the portfolio-scoped list.

  Writes: `StrategicInitiatives_AddKpiMeasurement` and `_RemoveKpiMeasurement`. Recording a KPI measurement is periodic, low-blast-radius reporting work, so it follows the precedent set by project health checks. Everything else stays read-only — initiatives cannot be created, updated, or deleted, and KPIs cannot be added, edited, reordered, or deleted. (Initiative _status_ transitions land separately in this same release.)

  Note the ID asymmetry: the read tools accept an ID **or** a key for both the initiative and the KPI, while the two measurement write tools take UUIDs only and cross-check the body against the path.

  Three domain behaviours are documented in the `wayd-ppm` skill because they are easy to get backwards. A KPI's `targetDirection` may be Decrease, where a falling value is improvement, so lower is not universally worse. A KPI's headline `actualValue` is the measurement with the latest measurement _date_, not the most recently entered one, so back-dating does not change it. And measurement dates must be unique within a KPI, so re-submitting an existing date is rejected rather than treated as an update.

- e5a4b12: Add create and update tools for portfolios, programs, and projects, completing PPM record management.

  Creates: `Portfolios_Create`, `Programs_Create`, `Projects_Create`. New records start in Proposed — creating one does not activate it.

  Updates: `Portfolios_Update`, `Programs_Update`, `Projects_Update`, plus `Projects_ChangeProgram` (move a project between programs, or pass null to detach it) and `Projects_ChangeKey`. Also fills three gaps found along the way: `Projects_UpdateProjectHealthCheck`, `Projects_DeleteProjectHealthCheck`, and `ExpenditureCategories_GetOptions`, which resolves the category id that project create and update both require.

  **Updates are whole-record overwrites, not patches**, matching the API's `PUT` semantics. A field omitted from the body is cleared rather than preserved, so every update tool tells the caller to read the record first and echo back what should stay the same.

  The sharp edge is role assignments. `sponsorIds`, `ownerIds`, `managerIds`, and `memberIds` replace the membership for that role, and a list that is omitted **or empty** removes everyone currently holding it — there is no way to express "leave this role alone". A minimal-looking `Projects_Update` carrying only a new name will strip every sponsor, owner, manager, and member. That is worth stating plainly because Owners and Managers are precisely who is authorised to manage PPM records, so an incautious update can leave a project nobody but a domain administrator can edit. The warning appears on every role-list field, in each update tool's description, and in the `wayd-ppm` skill.

  Updates and the two project-identity changes are annotated `destructiveHint` so clients confirm first; creates are not, since they add a record rather than overwriting one. Deleting a portfolio, program, or project remains unavailable through MCP.

  Also fixes a latent bug in the stdio test harness. It counted newline-split segments to decide when the server had finished responding, so a chunk ending mid-line counted as a complete response and the child was killed partway through writing. The tools/list payload only recently grew past the pipe buffer, which is what exposed it; the harness now counts newline-terminated lines only.

- 2a9c62d: Add project scoring and portfolio ranking read tools.

  - `Projects_GetScoringContext` — the scoring model assigned to a project's portfolio, whether that model is archived, and the project's current score.
  - `Projects_GetScores` — a project's full scoring history, headline values only.
  - `Projects_GetScore` — one score in full, including every criterion rating and computed output in the frozen snapshot recorded at scoring time.
  - `Portfolios_GetRankingScoreboard` — the score breakdown behind a portfolio's ranking board: the model definition plus per-project ratings and outputs.

  All four are read-only and take UUIDs rather than keys. Recording a score and reordering ranks remain unavailable through MCP.

  Two behaviours worth knowing, both documented in the `wayd-ppm` skill: a project appears in the scoreboard with empty ratings and outputs when it is unscored **or** when its latest score used a different or older model than the portfolio's current one, so empty does not mean "scored zero"; and the scoreboard returns breakdowns keyed by project ID without names or positions, so it needs joining against the portfolio's project list to be readable.

- 30eddcf: Add status transition tools, and tool annotations so clients confirm before running them.

  Fourteen transitions are now reachable: portfolio activate / close / archive, program activate / complete / cancel, project approve / activate / complete / cancel, and strategic initiative approve / activate / complete / cancel. Each takes a UUID and no body, matching the API's dedicated action endpoints — status is never set by writing a field.

  These are the first tools that change a published status other people rely on, so the server now emits the MCP `ToolAnnotations` that clients use to decide what to confirm. Every transition is marked `destructiveHint`, as are the eleven existing delete and remove tools across story maps, tasks, and KPI measurements, which previously carried no hint at all and so would run without a prompt. GET-backed tools are advertised `readOnlyHint`; any tool that is not a GET must opt in explicitly, so a future write tool can never be silently advertised as safe. A protocol-level test asserts the contract over the wire and fails with a named tool if a hint is ever dropped.

  The annotation is advisory — it tells a client to ask and cannot force one to. Server-side authorization is unchanged and remains the actual control: portfolio, program, and project transitions still require delivery leadership (Owner or Manager on the record or an ancestor), while initiative transitions check the permission claim only.

  Two side effects are documented on the tools themselves and in the `wayd-ppm` skill, because neither is visible from the endpoint name. **Activating a portfolio sets its start date to today and closing it sets its end date to today**, with no way to backdate through these calls — so they must not be used to tidy up a record that really started or ended on another date. And **completing or cancelling a strategic initiative closes it**, after which its KPIs and linked projects can no longer be modified.

  The skill also documents the prerequisites that cause rejections, most notably that a program cannot be completed or cancelled from Active until every project inside it is already closed.

- 2a9c62d: Add four PPM read tools and fix array query parameter serialization.

  New tools:

  - `Projects_GetMyProjectsSummary` — the caller's project involvement as counts per role, answering "what am I on?" without listing and filtering every project.
  - `Projects_GetMyProjectsTaskMetrics` — aggregated overdue / due-this-week / upcoming open-task counts across the caller's projects.
  - `Projects_GetStatusHistory` — a project's status change history, including who made each change, when, and why. Takes a project UUID; unlike most project endpoints it does not accept a key.
  - `Projects_GetProjectsPlanSummaries` — plan summary metrics for many projects in one request. The per-project version was already exposed, so surveying twenty projects previously took twenty calls.

  Fixes array query parameters, which were silently ignored server-side. Axios serialises arrays as `status[]=1&status[]=2`, but ASP.NET's model binder reads repeated bare keys (`status=1&status=2`) and binds the bracketed form to nothing. Every array-valued filter — `status` and `role` on the project, program, and portfolio list tools — was therefore dropped, returning unfiltered results rather than an error. The executor now serialises without bracket indexes, matching the generated API client, and a test pins the wire format.

## 0.4.0

### Minor Changes

- f2ae7f2: Update dependencies and raise the minimum supported Node version to 22.

  - **Node**: `engines.node` is now `>=22.0.0` (was `>=20.0.0`). Node 20 reaches end-of-life in April 2026; Node 22 and 24 remain supported.
  - **Dependencies**: `@modelcontextprotocol/sdk` 1.30, `axios` 1.19, `dotenv` 17, `zod` 4.
  - **Tooling**: ESLint 10, `@eslint/js` 10, `globals` 17, `@changesets/cli` 2.31, `@types/node` 24, `tsx` 4.23, `typescript-eslint` 8.67.

  Fixes a stdout corruption bug surfaced by the dotenv 17 upgrade: dotenv now prints a startup banner to stdout by default, which broke the stdio transport's JSON-RPC stream. It is now loaded with `quiet: true`, keeping stdout reserved for the protocol.

  Adds a test suite (`npm test`) covering the stdio transport, error formatting, and generated schema validation. It runs on Node's built-in test runner, so it adds no dependencies, and is wired into CI. The headline test asserts that stdout carries nothing but JSON-RPC — the failure that lint, typecheck, and build all missed above.

  Tightens type checking with `noUncheckedIndexedAccess`, so dynamic key lookups yield `T | undefined`, and drops `noImplicitAny`/`strictNullChecks` from the compiler options since `strict` already implies both.

  Moves the generated schemas onto Zod 4's top-level format validators (`z.uuid()`, `z.iso.date()`, `z.iso.datetime()`, `z.ZodType`) in place of the deprecated string-method forms. Each replacement was checked to validate identically to the form it replaces, and a test fails if the output ever drifts back onto a deprecated API.

  Replaces the `json-schema-to-zod` dependency with a small in-house emitter (`scripts/json-schema-to-zod.ts`). The upstream project was archived in April 2026 and still emitted Zod 3 syntax. The replacement covers exactly the JSON Schema subset the API produces and throws on anything it does not recognise, so an unsupported keyword fails the build instead of silently weakening argument validation. It generates all 106 schemas byte-identically to the previous output.

  Upgrades zod to 4, which renamed `ZodError.errors` to `.issues`. Argument validation read the removed property, so on zod 4 every validation failure would have thrown a `TypeError` instead of returning the "Invalid arguments" message that tells a model what to fix. Validation messages are unchanged in substance; zod's issue codes are more precise (for example `invalid_string` is now `invalid_format`).

## 0.3.0

### Minor Changes

- 3e9dd9c: Add MCP tools for reading, creating, and managing Story Maps.

  **Read tools:**

  - `StoryMaps_GetStoryMaps` — list story maps (id, key, name, description, status, owner), with an `includeArchived` option
  - `StoryMaps_GetStoryMap` — get a story map in full by ID or key: goals, each with ordered steps and tasks (including checklists, persona tags, and linked work item IDs), plus the map's swim lanes and personas

  **Management tools:**

  - Maps — `StoryMaps_CreateStoryMap`, `StoryMaps_UpdateStoryMap`, `StoryMaps_ChangeStoryMapOwner`, `StoryMaps_ArchiveStoryMap`, `StoryMaps_DeleteStoryMap`
  - Goals — `StoryMaps_AddGoal`, `StoryMaps_RenameGoal`, `StoryMaps_ReorderGoal`, `StoryMaps_DeleteGoal`, `StoryMaps_SetGoalPersonas`
  - Steps — `StoryMaps_AddStep`, `StoryMaps_RenameStep`, `StoryMaps_ReorderStep`, `StoryMaps_MoveStep`, `StoryMaps_DeleteStep`, `StoryMaps_SetStepPersonas`
  - Tasks — `StoryMaps_AddTask`, `StoryMaps_UpdateTask`, `StoryMaps_MoveTask`, `StoryMaps_DeleteTask`, `StoryMaps_SetTaskPersonas`
  - Checklists — `StoryMaps_AddChecklistItem`, `StoryMaps_RenameChecklistItem`, `StoryMaps_SetChecklistItemChecked`, `StoryMaps_RemoveChecklistItem`, `StoryMaps_PromoteChecklistItem`
  - Work item links — `StoryMaps_LinkWorkItem`, `StoryMaps_UnlinkWorkItem`
  - Swim lanes — `StoryMaps_AddSwimLane`, `StoryMaps_RenameSwimLane`, `StoryMaps_SetSwimLaneDates`, `StoryMaps_ReorderSwimLane`, `StoryMaps_RemoveSwimLane`
  - Personas — `StoryMaps_AddPersona`, `StoryMaps_UpdatePersona`, `StoryMaps_ReorderPersona`, `StoryMaps_DeletePersona`

  Also adds a `wayd-story-maps` agent skill covering story map analysis (scope per swim lane, persona coverage, checklist progress, work item traceability) and safe management workflows (UUID resolution, replace-set persona tagging, destructive-operation cautions).

## 0.2.0

### Minor Changes

- fab40f5: Add MCP tools for inspecting and creating health checks on planning interval objectives and PPM projects.

  **New tools:**

  - `PlanningIntervals_GetObjectiveHealthChecks` — get the full health check history for a PI objective (newest first)
  - `PlanningIntervals_GetObjectiveHealthCheck` — get a specific PI objective health check by ID
  - `PlanningIntervals_CreateObjectiveHealthCheck` — log a new health check on a PI objective (`statusId` 1=Healthy, 2=AtRisk, 3=Unhealthy); auto-expires the previously active check
  - `Projects_GetProjectHealthChecks` — get the full health check history for a project (newest first)
  - `Projects_GetProjectHealthCheck` — get a specific project health check by ID
  - `Projects_CreateProjectHealthCheck` — log a new health check on a project (`status` "Healthy" / "AtRisk" / "Unhealthy"); auto-expires the previously active check

## 0.1.0

### Minor Changes

- bcba8b3: Add project lifecycle tools, new project read endpoints, and remove the deprecated task tree tool.

  **New tools:**

  - `ProjectLifecycles_GetProjectLifecycles` — list project lifecycles with optional state filter (Proposed/Active/Archived)
  - `ProjectLifecycles_GetProjectLifecycle` — get project lifecycle details including phases
  - `Projects_GetProjectTeam` — get team members for a project with their roles, assigned phases, and active workload
  - `Projects_GetProjectPhases` — get all phases for a project
  - `Projects_GetProjectPhase` — get details for a specific project phase
  - `Projects_GetProjectPlanTree` — get a unified plan tree with phases as top-level nodes and tasks nested within (replaces the removed task tree endpoint)
  - `Projects_GetProjectPlanSummary` — get summary metrics for a project's plan (overdue, due this week, upcoming, total task counts)

  **Updated tools:**

  - `Projects_GetProjects` — added `role` query filter to scope results to projects where the current user holds a specific role (Sponsor/Owner/Manager/Member)

  **Removed tools:**

  - `Tasks_GetProjectTaskTree` — the underlying API endpoint was removed; use `Projects_GetProjectPlanTree` instead for a richer unified view of phases and tasks
