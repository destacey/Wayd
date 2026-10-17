# Composite MCP Tool Candidates

Audit for [#972](https://github.com/destacey/Wayd/issues/972), consumed by [#975](https://github.com/destacey/Wayd/issues/975). Part of epic [#962](https://github.com/destacey/Wayd/issues/962).

## Verdict

The skills spend most of their words on two things a server can do better: **read-merge-write updates**, where an agent must read a record and echo back every field and role list or lose data, and **evidence-gathering recipes**, where an agent calls five to eight read tools in a fixed order before it can make a judgment. Both become composite tools. Everything that depends on what an earlier call returned — classifying health, choosing what to resubmit, deciding whether a version or a release is meant — stays in the skills.

Twenty candidates follow, in three tiers:

- **Tier 1, six write tools that remove an overwrite trap at its source.** Role changes and partial detail updates on portfolios, programs and projects; partial task updates; release contents; package manifests; story map persona tags.
- **Tier 2, eleven read-only tools** that replace an evidence or lookup recipe. The two health-check evidence tools the issue owner proposed lead.
- **Tier 3, three write sequences** with no overwrite trap, worth building only after the first two tiers.

Two prerequisites block #975 before any candidate can ship or be measured (see [Prerequisites](#prerequisites-for-975)): the tool-parity test pins the hosted tool list to the npm package's, and the evaluation stub serves the npm package's tools.

## Scope and non-goals

- This is an audit. No tool is implemented here; each candidate becomes part of #975.
- Every skill in `skills/` was classified: `wayd-ppm` (with `health-checks.md`, `kpis.md`, `scoring.md`), `wayd-pi`, `wayd-teams`, `wayd-delivery`, `wayd-products`, `wayd-imports`, `wayd-roadmaps`, `wayd-story-maps`, `wayd-users`.
- Every command, query, permission and authorization rule named below was checked against the code. Where a skill and the code disagree, it is listed under [Findings](#findings-where-a-skill-and-the-code-disagree), not silently resolved.
- The evaluation was not run. Each candidate names the scenarios in `Wayd.Web/src/Wayd.Mcp/evals/scenarios/` that exercise the sequence it replaces, so #975 can measure before and after.

## How a composite tool is built

### A tool is still a controller action

#975 describes composites as `[McpServerTool]` methods. Build them as controller actions marked `[McpTool]` instead, like every other hosted tool:

- `ApiPipeline` then applies authentication, `[MustHavePermission]`, `[FeatureGate]` and model validation exactly as for the HTTP request. A hand-written `McpServerTool` bypasses all of them and would have to repeat each one.
- The input schema and description come from the OpenAPI operation, so the composite is documented and typed like every other endpoint, and the same operation serves the web client if it wants it.
- A composite that reads across two resources stacks the attributes: `MustHavePermissionAttribute` derives from `AuthorizeAttribute`, so several on one action must all pass.
- A feature flag that gates only part of a read (the delivery forecast inside an evidence tool) is checked in the handler through `IFeatureFlags`, returning that section as unavailable rather than failing the whole call.

The two hand-written import tools (`Mcp/ImportTools.cs`) remain the exception, because they front every import endpoint at once.

### Writes are one new command, not a chain

Every existing command handler calls `SaveChangesAsync` itself, and several catch exceptions and reload the entity on a domain failure. Chaining a query and a command from a controller is therefore two units of work with a gap in which someone else's change can land, and chaining two commands is two commits.

Each write composite is **one new command** whose handler loads the aggregate once, merges the request into its current state, and calls the domain method the existing command already calls. It saves once. The candidates below name the existing query and command each one replaces for the agent and the domain method its new command calls.

`BaseDbContext.BeginUnitOfWork` spans several saves, but only `IWorkDbContext` exposes it today; no PPM, Planning or ProductManagement interface does. None of the tier 1 candidates need it. Tier 3 candidates reuse the import handlers, which already walk a record through several states in one save.

### PPM writes keep the delivery-leadership rule

A PPM composite follows the Authorization section of the root `AGENTS.md`, as `UpdateProjectCommand` does:

1. The command is `IRequireLinkedEmployee`.
2. The handler injects `ICurrentPrincipal` and `ICurrentUser` and calls `ResolvePpmActor`.
3. The query includes `.Include(p => p.Portfolio).ThenInclude(p => p!.Roles)` and, for a project, `.Include(p => p.Program).ThenInclude(p => p!.Roles)`.
4. The domain method receives `actor` and `project.AncestryRoles()`.

Project task changes are permission-only (`UpdateProjectTaskCommand` is not `IRequireLinkedEmployee`), so a task composite must not add the leadership check either.

### Confirmation: `validateOnly` or a separate preview

Every skill that can change data opens with "Before any change": describe the change, wait for the go-ahead, then do only what was approved. A composite has to leave room for that.

- **Write composites take `validateOnly`**, the name the import endpoints already use. With it set, the handler loads, merges, runs the validator and the domain method's authorization and rules, and returns the resolved change — each field and list before and after, with people who would lose a role named — without calling `SaveChangesAsync`. The agent shows that to the user, then calls again without the flag. The tool keeps its destructive annotation, so clients that confirm destructive calls still ask.
- **Creates that follow an evidence read** (logging a health check) need no flag: the tier 2 evidence tool is the preview, and the write stays the existing create tool. This is the `Imports_Preflight` → `Imports_Apply` split.
- **Deletes stay separate destructive tools.** A read-only impact tool is the preview.

### Prerequisites for #975

1. **Tool parity.** `McpToolCatalogTests` asserts the hosted tool names equal the npm package's (`Wayd.Web/tests/Wayd.Web.Api.IntegrationTests/Sut/McpToolCatalogTests.cs:37`). A hosted-only composite fails it. Either add each composite to `@wayd/mcp` as well, relax the test to "every npm tool is hosted", or ship composites after #979 retires the package.
2. **The evaluation stub.** `evals/stub-server.ts` advertises the tools in the npm package's `build/` (README, Known limits). A composite cannot be offered to the agent, and so cannot be measured, until the stub serves the hosted catalogue.
3. **Scenario assertions.** Several scenarios assert `called` on today's tools (`Projects_Update`, `Releases_SetContents`, `PlanningIntervals_GetObjectiveWorkItems`). Each needs an alternative accepting the composite, and scenarios that assert a change was *not* made need a matching `notCalled` for the composite. Make these edits in a new variant so the old one still runs beside it.

## Candidates

### Summary

| # | Tool | Kind | Replaces | Removes overwrite trap | Scenarios |
|---|---|---|---|---|---|
| 1 | `Projects_ChangeRoles`, `Programs_ChangeRoles`, `Portfolios_ChangeRoles` | Write | ppm "Updating a record", users "Resolve name" use | **Yes** | `users-add-project-manager` |
| 2 | `Projects_ChangeDetails`, `Programs_ChangeDetails`, `Portfolios_ChangeDetails` | Write | ppm "Updating a record (checklist)" | **Yes** | `ppm-rename-project` |
| 3 | `Tasks_ChangeProjectTask` | Write | ppm "Updating a task" | **Yes** | `users-assign-task-no-employee` |
| 4 | `Releases_ChangeContents` | Write | delivery "Overwrite, not patch" | **Yes** | `delivery-add-package-to-release`, `delivery-announce-unshipped-content` |
| 5 | `ReleasePackages_ChangeManifest` | Write | delivery "Recording what shipped" step 3 | **Yes** | none (gap) |
| 6 | `StoryMaps_ChangePersonaTags` | Write | story-maps "Persona sets replace" | **Yes** | `story-maps-add-task-persona`, `story-maps-persona-coverage` |
| 7 | `Projects_GetHealthCheckEvidence` | Read | health-checks.md checklist steps 1–6 | No | none (gap) |
| 8 | `PlanningIntervals_GetObjectiveHealthCheckEvidence` | Read | pi health-check recipe step 1 | No | `pi-recommend-health-check`, `pi-log-objective-health-check` |
| 9 | `PlanningIntervals_GetHealthSummary` | Read | pi health report step 1 | No | `pi-health-report` |
| 10 | `Versions_GetDeliveryTrail` | Read | delivery "Where did this version go" | No | `delivery-where-did-version-go` |
| 11 | `Products_GetDeletionBlockers` | Read | products "Deleting a product: four checks" | No | none |
| 12 | `Users_FindEmployees` | Read | users "Resolve a name" and reverse lookup | No | `users-add-project-manager`, `users-colleague-managed-projects`, `users-assign-task-no-employee` |
| 13 | `Imports_GetRunReport` | Read | imports flow steps 5–6, "Why a run failed" | No | `imports-why-partial-run`, `imports-versions-preflight-then-apply` |
| 14 | `Roadmaps_GetItemsInPeriod` | Read | roadmaps "What is planned for a period" steps 2–4 | No | `roadmaps-planned-for-quarter` |
| 15 | `Roadmaps_FindItems` | Read | roadmaps "Identifiers" lookup | No | `roadmaps-item-details-by-name`, `roadmaps-move-milestone-unsupported` |
| 16 | `StoryMaps_GetRemovalImpact` | Read | story-maps "Destructive operations" steps 1–2, 5 | No | `story-maps-delete-goal` |
| 17 | `Products_PreviewTag` | Read | products "Single-value axis silently replaces" | No | `products-tag-single-value-axis` |
| 18 | `Versions_Record` | Write | delivery "Recording what shipped" steps 1–2 | No | `delivery-record-version-not-release` |
| 19 | `Deployments_Record` | Write | delivery "Recording what shipped" step 7 | No | none |
| 20 | `StoryMaps_AddGoal` with `stepNames` | Write | story-maps "Building a map" steps 2–3 | No | none |

Paths below are shortened: `PPM/` is `Wayd.Services/Wayd.ProjectPortfolioManagement/src/Wayd.ProjectPortfolioManagement.Application/`, `Planning/` is `Wayd.Services/Wayd.Planning/src/Wayd.Planning.Application/`, `Work/` is `Wayd.Services/Wayd.Work/src/Wayd.Work.Application/`, `PM/` is `Wayd.Services/Wayd.ProductManagement/src/Wayd.ProductManagement.Application/`, `Common/` is `Wayd.Common/src/Wayd.Common.Application/`.

### Tier 1: writes that remove an overwrite trap

#### 1. `Projects_ChangeRoles` (and `Programs_ChangeRoles`, `Portfolios_ChangeRoles`)

- **Replaces:** wayd-ppm "Updating a record (checklist)" for role changes, and the main use of wayd-users ("add Priya as a manager"). Removes the trap the server instructions and `Projects_Update`'s description spend most words on: an omitted or empty role list removes everyone in that role.
- **Input:** `id` (UUID or key); for each of `sponsorIds`, `ownerIds`, `managerIds`, `memberIds` an `add` and a `remove` list of employee ids (portfolios have no members); `validateOnly`.
- **Agent's sequence today:** `GetProjectQuery` (`PPM/Projects/Queries/GetProjectQuery.cs`) → `UpdateProjectCommand` (`PPM/Projects/Commands/UpdateProjectCommand.cs`), mapping `projectManagers[].id` to `managerIds`, `start`/`end` to the date range and `expenditureCategory.id` to `expenditureCategoryId` on the way.
- **New command:** `ChangeProjectRolesCommand : ICommand, IRequireLinkedEmployee`. Loads `Projects.Include(Roles)` plus ancestor roles as `UpdateProjectCommand` does, builds the current role map from `project.Roles`, applies the deltas, and calls `Project.UpdateRoles(actor, ancestry, merged, now)` (`Project.cs:317`). `UpdateRoles` stays the single entry point its documentation describes; the composite only computes the set. Programs call `Program.UpdateRoles` with portfolio roles loaded; portfolios call `ProjectPortfolio.UpdateRoles`, which takes no ancestry.
- **Confirmation:** `validateOnly` returns each role before and after, naming anyone removed. Removing the last Owner and Manager is allowed by the domain today; the preview should call it out.
- **Why not chain into `UpdateProjectCommand`:** it rewrites name, description, dates, themes and expenditure category from a DTO round trip just to change one role.

#### 2. `Projects_ChangeDetails` (and `Programs_ChangeDetails`, `Portfolios_ChangeDetails`)

- **Replaces:** wayd-ppm "Updating a record (checklist)" steps 1–4 and 7 for everything except roles.
- **Input:** `id` (UUID or key) and any of `name`, `description`, `businessCase`, `expectedBenefits`, `expenditureCategoryId`, `start`/`end`, `strategicThemeIds` (as `add`/`remove`); omitted fields are kept. `validateOnly`.
- **Agent's sequence today:** `GetProjectQuery` → `UpdateProjectCommand`. Programs: `GetProgramQuery` → `UpdateProgramCommand`; portfolios: `GetProjectPortfolioQuery` → `UpdateProjectPortfolioCommand`.
- **New command:** loads as `UpdateProjectCommand` does and calls only the domain methods whose inputs changed — `UpdateDetails`, `UpdateTimeline`, `UpdateStrategicThemes` — never `UpdateRoles`. A rename can then never touch the role lists.
- **Clearing a field** needs an explicit signal distinct from omission (JSON `null` versus absent, or a `clear` list). Decide in #975; the clearable fields are `businessCase`, `expectedBenefits` and the date range.

#### 3. `Tasks_ChangeProjectTask`

- **Replaces:** wayd-ppm "Updating a task" steps 1–2, including the `parentId`-or-`projectStageId` rule and "an omitted or empty `assigneeIds` removes every assignee".
- **Input:** `projectIdOrKey`, `taskIdOrKey`; any of the task's fields; `addAssigneeIds`/`removeAssigneeIds`; `validateOnly`.
- **Agent's sequence today:** `GetProjectTaskQuery` (`PPM/ProjectTasks/Queries/GetProjectTaskQuery.cs`) → `UpdateProjectTaskCommand` (`PPM/ProjectTasks/Commands/UpdateProjectTaskCommand.cs`).
- **Precedent:** `ProjectTasksController.PatchProjectTask` already reads the task, applies a JSON Patch to `UpdateProjectTaskRequest.FromDto(...)`, validates and sends `UpdateProjectTaskCommand` — the merge exists, but as JSON Patch, which agents handle poorly, and with no `[McpTool]`. The composite is the same merge with a plain partial body, ideally moved into one command so the read and write share a unit of work.
- **Authorization:** permission only (`Update` on `Projects`), as today.

#### 4. `Releases_ChangeContents`

- **Replaces:** wayd-delivery "Overwrite, not patch", "Recording what shipped" step 5, and wayd-products purge step 2.
- **Input:** `id`; `addVersionIds`, `removeVersionIds`, `addPackageIds`, `removePackageIds`; `validateOnly`. Moving a version into a package is one call (remove the version, add the package), so the "a version is announced once" check runs against the end state.
- **Agent's sequence today:** `GetReleaseQuery` (`PM/Releases/Queries/GetReleaseQuery.cs`) → `SetReleaseContentsCommand` (`PM/Releases/Commands/SetReleaseContentsCommand.cs`), mapping `versions[].version.id` to `versionIds` and `packages[].package.id` to `packageIds`.
- **New command:** `ChangeReleaseContentsCommand : IRequireLinkedEmployee`, reusing `SetReleaseContentsCommand`'s load (`Releases.Include(Versions).Include(Packages)`) and its versions-in-packages resolution, then `Release.SetContents`. A frozen release is refused as it is today.

#### 5. `ReleasePackages_ChangeManifest`

- **Replaces:** wayd-delivery "Recording what shipped" step 3 and the overwrite table row for manifests; wayd-products purge step 3.
- **Input:** `id`; `upsert` entries keyed by `productId` (`versionId`, `version`, `kind`) — the manifest allows one line per product; `removeProductIds`; `validateOnly`.
- **Agent's sequence today:** `GetReleasePackageQuery` (`PM/ReleasePackages/Queries/GetReleasePackageQuery.cs`) → `SetReleasePackageManifestCommand` (`PM/ReleasePackages/Commands/SetReleasePackageManifestCommand.cs`).
- **Silent data loss it removes:** the read shape is `components[].product.id` and `components[].versionRecord.id`; the write shape is `productId` and `versionId`. An agent that echoes `versionRecord` back sends no `versionId`, and the release no longer knows the version is inside the package.
- **New command:** loads `ReleasePackages.Include(Components)`, merges, calls `ReleasePackage.SetManifest`. It should also enforce "a version is announced once" against unannounced releases that list the package, which `SetManifest` does not do today (finding D3).

#### 6. `StoryMaps_ChangePersonaTags`

- **Replaces:** wayd-story-maps "Persona sets replace" and the read → merge → `StoryMaps_SetTaskPersonas` sequence; likewise for goals and steps.
- **Input:** `storyMapId`, `targetType` (`Goal`, `Step`, `Task`), `targetId`, `addPersonaIds`, `removePersonaIds`, `validateOnly`.
- **Agent's sequence today:** `GetStoryMapQuery` (`Planning/StoryMaps/Queries/GetStoryMapQuery.cs`) → `SetTaskPersonasCommand` / `SetGoalPersonasCommand` / `SetStepPersonasCommand` (`Planning/StoryMaps/Commands/`).
- **New command:** loads the map as `SetTaskPersonasCommandHandler` does, merges, calls `StoryMap.SetTaskPersonas` (or the goal and step equivalents), inside `StoryMapMutation.Apply` so the map's row version is checked. The existing `Set*Personas` handlers skip `StoryMapMutation` and are last-write-wins.
- An add-only call purely adds tags; the tool can still not be `Destructive = false`, since the same tool removes.

### Tier 2: read-only evidence and lookups

#### 7. `Projects_GetHealthCheckEvidence`

Proposed by the issue owner on #975.

- **Replaces:** `health-checks.md` "Recommending a health check" steps 1–6. Steps 7–8 (classify, draft, re-check) stay in the skill; the write stays `Projects_CreateProjectHealthCheck` after the user confirms.
- **Input:** `idOrKey`; optional forecast options (`lookbackDays`, `ignoreDependencies`, `startedWorkFirst`).
- **Dispatches:**
  1. `GetProjectQuery` (`PPM/Projects/Queries/GetProjectQuery.cs`) — status, committed `start`/`end`, current `healthCheck`, roles.
  2. `GetProjectPlanSummaryQuery` (`PPM/Projects/Queries/GetProjectPlanSummaryQuery.cs`) — overdue, due this week, upcoming, total leaf tasks.
  3. `GetProjectTasksQuery` (`PPM/ProjectTasks/Queries/GetProjectTasksQuery.cs`) — returned filtered to the overdue tasks, with keys, so the note cites real tasks.
  4. `GetProjectStagesQuery` (`PPM/Projects/Queries/GetProjectStagesQuery.cs`) when a lifecycle is assigned — with stages past their end and not complete flagged.
  5. `GetProjectForecastQuery` (`Work/WorkItems/Queries/GetProjectForecastQuery.cs`) with the project's `end` as target, only when `IFeatureFlags` reports `DeliveryForecasting` on; otherwise `forecast: null` with the reason.
  6. `GetProjectHealthChecksQuery` (`PPM/Projects/HealthChecks/Queries/GetProjectHealthChecksQuery.cs`) — the latest check.
  7. `GetProjectActivitiesQuery` (`PPM/Projects/Queries/GetProjectActivitiesQuery.cs`) — paged back to the latest check's `reportedOn`; the query has no "since" filter.
- **Critical path:** `GetCriticalPathQuery` is a stub that always returns an empty list (`PPM/ProjectTasks/Queries/GetCriticalPathQuery.cs`, TODO in the handler). The composite reports critical-path evidence as unavailable rather than calling it, which also removes the skill's warning about reading an empty list as "no critical tasks".
- **Authorization:** `View` on `Projects` — the same permission each step's endpoint requires today.
- **Scenarios:** none exercise this recipe. `ppm-log-health-check` gives the status in the prompt and asserts only the confirmed create. #975 needs a "recommend a project health check" scenario to measure this candidate.

#### 8. `PlanningIntervals_GetObjectiveHealthCheckEvidence`

Proposed by the issue owner on #975.

- **Replaces:** wayd-pi health-check recipe step 1, the "fetch the PI for default dates" fallback, and the "a 404 means forecasting is off" inference. Steps 2–3 (formulate, present) stay; the write stays `PlanningIntervals_CreateObjectiveHealthCheck`.
- **Input:** `idOrKey`, `objectiveIdOrKey` (UUID or key throughout; today `GetObjectiveHealthChecks` needs both UUIDs); optional forecast options and `includeClosedRisks`.
- **Dispatches:**
  1. `GetPlanningIntervalQuery` (`Planning/PlanningIntervals/Queries/GetPlanningIntervalQuery.cs`).
  2. `GetPlanningIntervalObjectiveQuery` (`Planning/PlanningIntervals/Queries/GetPlanningIntervalObjectiveQuery.cs`) — details, dates, team, active check. Also proves the objective belongs to the PI (finding P1).
  3. `GetExternalObjectWorkItemsQuery` (`Work/WorkItems/Queries/GetExternalObjectWorkItemsQuery.cs`) — progress summary and items, sorted by stack rank as the controller does.
  4. `GetExternalObjectWorkItemMetricsQuery` (`Work/WorkItems/Queries/GetExternalObjectWorkItemMetricsQuery.cs`) — over PI start to the earlier of today and PI end, the controller's window.
  5. `GetExternalObjectForecastQuery` (`Work/WorkItems/Queries/GetExternalObjectForecastQuery.cs`) with the objective's target date or the PI's end, only when `DeliveryForecasting` is on.
  6. `GetPlanningIntervalObjectiveHealthChecksQuery` (`Planning/PlanningIntervals/HealthChecks/Queries/`).
  7. `GetPlanningIntervalObjectiveActivitiesQuery` (`Planning/PlanningIntervals/Queries/`) — paged back to the last check; no "since" filter exists.
  8. `GetRisksByPlanningIntervalQuery` (`Planning/Risks/Queries/`) filtered to the objective's team. Risks link to a team, not an objective, so "risks to this objective" can only mean the team's open risks.
- **Derived values worth returning:** effective start and target dates with a "defaulted" flag, the elapsed share of the objective's window, and the progress change since the last check.
- **Authorization:** `View` on `PlanningIntervals` and on `PlanningIntervalObjectives`, stacked.
- **Scenarios:** `pi-recommend-health-check` (its fixtures are exactly this sequence; its `called PlanningIntervals_GetObjectiveWorkItems` assertion needs an alternative), `pi-log-objective-health-check`.

#### 9. `PlanningIntervals_GetHealthSummary`

- **Replaces:** wayd-pi PI health report step 1, and the grouping in step 2.
- **Input:** `idOrKey`, optional `teamId`.
- **Dispatches:** `GetPlanningIntervalQuery`; `GetPlanningIntervalObjectivesQuery` mapped through `PlanningIntervalObjectiveHealthCheckDto.Create` as the health-report action does; `GetPlanningIntervalPredictabilityQuery`; `GetRisksByPlanningIntervalQuery` (open risks).
- **Returns:** objectives grouped Unhealthy, At Risk, Healthy, no active check; elapsed share of the PI (not carried by `PlanningIntervalDetailsDto`); predictability; open risks. Ordering risks by exposure needs the domain value: `RiskListDto.Exposure` is a string.
- **Scenarios:** `pi-health-report` (asserts `called PlanningIntervals_GetObjectivesHealthReport`; needs an alternative).

#### 10. `Versions_GetDeliveryTrail`

- **Replaces:** wayd-delivery "Where did this version go".
- **Input:** `versionIdOrKey`.
- **Dispatches:** `GetVersionQuery` (`PM/Versions/Queries/`); `GetReleasePackagesQuery` filtered to packages naming the version; `GetDeploymentsQuery` for the version and once per package (it takes one version or one package); `GetReleasesQuery` with `containingVersionId` (covers both direct and through-package contents); optionally `GetEnvironmentRolloutQuery` filtered to the version's product.
- **Scenarios:** `delivery-where-did-version-go` (assertions are `says` checks and survive a composite).

#### 11. `Products_GetDeletionBlockers`

- **Replaces:** wayd-products "Deleting a product: four checks", purge step 1 and its loop, and "Why can't I delete this". `Products_Delete` reports only the first blocker it meets (`Product.cs`, delete guard).
- **Input:** `productIdOrKey`, `includeCascade`.
- **Dispatches:** `GetProductQuery`, `GetProductsQuery` (children), `GetVersionsQuery`, `GetReleasePackagesQuery` (packages naming the product), and a direct dependency check; with `includeCascade`, the releases and deployments a purge would touch.
- **Gaps:** no query lists a product's *direct* dependencies — `GetProductDependenciesQuery` always rolls up the subtree — and `GetReleasesQuery` cannot filter by package. Both need adding (finding D4, D5).

#### 12. `Users_FindEmployees`

- **Replaces:** wayd-users "Resolve a name to an employee id" steps 1–2 and the reverse lookup. Steps 3–5 (no employee, inactive, several matches) stay judgment; the tool reports them, it does not decide.
- **Input:** `names` and/or `employeeIds`.
- **Returns per name:** matches with `employeeId`, `employeeKey`, `name`, `userId`, `isActive`, `hasEmployee`, plus `unmatched` and `ambiguous`.
- **Source:** `Users_GetUsers` does not go through the dispatcher; it calls `IUserService.GetListAsync` (`Common/Identity/Users/IUserService.cs`). `GetEmployeesQuery` (`Common/Employees/Queries/GetEmployeesQuery.cs`) covers employees without an account and needs `View` on Employees instead of Users, which also covers the skill's fallback when `Users_GetUsers` is denied. Neither searches by name; the composite filters in memory.

#### 13. `Imports_GetRunReport`

- **Replaces:** wayd-imports flow steps 5–6 (poll, then page the failed rows) and "Finding an existing import" after the run is chosen.
- **Input:** `id`.
- **Dispatches:** `GetImportProcessQuery`, then `GetImportProcessRowsQuery` for `Failed` rows, every page (`Common/Imports/Queries/`). Rows with warnings cannot be filtered today; the query filters by status only.
- **Also:** `Imports_Preflight` already waits for the run (`ImportSubmissionResponder`); returning the same report once the run is terminal removes the poll entirely.
- **Not a candidate:** preflight → apply as one call. The order is fixed but the gate is judgment (user approval, and zero rejections for an Atomic import); keeping `Imports_Apply` separate is the confirmation step.

#### 14. `Roadmaps_GetItemsInPeriod`

- **Replaces:** wayd-roadmaps "What is planned for a period" steps 2–4 (get the items, flatten the tree, filter by overlap, sort). Choosing the roadmap stays.
- **Input:** `idOrKey`, `start`, `end`.
- **Dispatches:** `GetRoadmapQuery` (does the roadmap cover the period), `GetRoadmapItemsQuery`, then the overlap filter in memory (`Planning/Roadmaps/Queries/`).
- **Returns:** a flat list with `type`, dates and parent path, plus a `coversPeriod` flag.

#### 15. `Roadmaps_FindItems`

- **Replaces:** the wayd-roadmaps "Identifiers" lookup: `itemId` takes a UUID only, so the agent walks `Roadmaps_GetItems` to find it by name.
- **Input:** `idOrKey`, `name` (case-insensitive contains).
- **Dispatches:** `GetRoadmapItemsQuery`, flatten and filter, then `GetRoadmapItemQuery` per match.

#### 16. `StoryMaps_GetRemovalImpact`

- **Replaces:** wayd-story-maps "Destructive operations" steps 1–2 and 5 (read the map, count what goes with the target, re-read). Step 3, confirming, stays; the delete stays its own destructive tool.
- **Input:** `storyMapIdOrKey`, `targetType` (map, goal, step, task, swim lane, persona), `targetId`.
- **Dispatches:** `GetStoryMapQuery`, then counts. Persona counts mirror `StoryMap.CountPersonaTags`.

#### 17. `Products_PreviewTag`

- **Replaces:** wayd-products "Tagging a single-value axis silently replaces". Not an overwrite trap: `Products_Tag` adds one tag, but on an axis with `allowsMany = false` it replaces the existing one without saying so.
- **Input:** `productIdOrKey`, `tagId`.
- **Dispatches:** `GetProductQuery` and `GetProductTagCategoriesQuery` (`PM/ProductTagCategories/Queries/`).
- **Returns:** `allowsMany`, the tag that would be removed, and whether the tag and its category are active (an inactive one is refused).
- A cheaper alternative is a `validateOnly` flag on `Products_Tag` returning the same.

### Tier 3: write sequences without an overwrite trap

#### 18. `Versions_Record`

- **Replaces:** wayd-delivery "Recording what shipped" steps 1–2: plan, cut if `cutAt` is given, mark released if `releasedAt` is given.
- **Input:** `productId`, `version`, optional `name`, `targetDate`, `cutAt`, `releasedAt`; `validateOnly`.
- **Commands:** `PlanVersionCommand` → `CutVersionCommand` → `MarkVersionReleasedCommand` (`PM/Versions/Commands/`). As one save, reuse the walk in `ImportVersionsCommand`, which already moves a version to the state its dates describe.

#### 19. `Deployments_Record`

- **Replaces:** wayd-delivery "Recording what shipped" step 7, for a deployment recorded after the fact.
- **Input:** `versionId` or `packageId`, `environmentId`, `startedAt`, `outcome`, `completedAt`, optional `rolledBackAt`, `reason`.
- **Commands:** `StartDeploymentCommand` then `SucceedDeploymentCommand`, `FailDeploymentCommand`, or succeed then `RollBackDeploymentCommand` (`PM/Deployments/Commands/`). `ImportDeploymentsCommand` already does this walk.

#### 20. `StoryMaps_AddGoal` with `stepNames`

- **Replaces:** wayd-story-maps "Building a map" steps 2–3 (`AddGoalCommand` then `AddStepCommand` per step).
- One command calling `StoryMap.AddGoal` then `AddStep`, saved once. Fixing finding S1 matters more than this candidate.

## Not candidates: fixed better elsewhere

| Sequence | Better fix |
|---|---|
| ppm "Creating a task" step 1: three lookups for `typeId`, `statusId`, `priorityId` | The request models take `int` for closed sets the command already types as enums (`TaskStatus`, `TaskPriority`), against the root `AGENTS.md` rule. Take enum codes and the lookups go away. |
| pi "Notes for logging a check": `statusId` 1/2/3 | Same: `CreatePlanningIntervalObjectiveHealthCheckRequest.StatusId` is an `int` cast to `HealthStatus`. Take the enum code. |
| ppm "Prerequisites that cause rejections" for program complete | Have the refusal name the projects still open. The check is already server-side. |
| delivery "Announce checklist" | Judgment per entry. Have the `MarkReleased` refusal list the unshipped entries. |
| story-maps rename or describe a task via `StoryMaps_UpdateTask` (overwrites both) | `RenameTask` and `SetTaskDescription` endpoints exist without `[McpTool]`. Expose them. |
| kpis.md "Recording a measurement" | The domain already rejects a duplicate date. Let the add tool accept keys and return the KPI's new `actualValue` and `progress`. |
| ppm "Moving a project backwards", imports dependency order and Cancel/Resume/Retry, teams forecasts and work distribution, PI predictability | Judgment on what earlier calls returned. Stays in the skills. |

## Cross-tool rules for server instructions and tool descriptions

Server instructions are `Wayd.Web/src/Wayd.Mcp/src/instructions.ts` and `Wayd.Web/src/Wayd.Web.Api/Mcp/McpServerInstructions.cs`, held identical by the parity test.

| Rule | Status |
|---|---|
| Confirm **every** change, creates and tags included, and do only what was approved | **In the server instructions.** They previously covered only "status changes, deletes and other destructive tools", which missed `Destructive = false` creates such as `Projects_CreateProjectHealthCheck` |
| Every list parameter on a write replaces the set (`assigneeIds`, `strategicThemeIds`, `personaIds`, `versionIds`, `packageIds`), not only role lists | **In the server instructions** |
| Role, assignee and `employeeId` fields take employee ids, never user ids, unless a description says otherwise (a story map's owner is a user id) | **In the server instructions** |
| Moments are ISO 8601 datetimes with their offset, never converted to a local date; calendar dates are `yyyy-MM-dd` | **In the server instructions** |
| Read shapes differ from write shapes (`projectManagers[].id` → `managerIds`, `start`/`end` → date range, `versions[].version.id` → `versionIds`, `components[].versionRecord.id` → `versionId`, health status `"At Risk"` → `"AtRisk"`) | Open: each write tool's description. The tier 1 composites remove most of them |
| A team is addressed by id, key or code | Open: parameter descriptions |
| Tree results: walk `children`; an empty result may mean private | Open: roadmaps tool descriptions (skill only today) |
| Imports reference records by id or key, never by display name | Open: `Imports_Preflight` and `Imports_GetFileFormat` descriptions (skill only today) |
| Dependency changes take the product that has the dependency, not the product queried | In wayd-products; open for the dependency tool descriptions |
| Check `hasNextPage` before concluding something never happened | Covered by the tool descriptions |

## Findings: where a skill and the code disagree

Each was confirmed in the code. **Fixed** marks the ones corrected alongside this audit; the rest are open.

**PPM**

- **T1. Closed sets cross as `int`.** Open. `CreateProjectTaskRequest` and `UpdateProjectTaskRequest` take `TypeId`, `StatusId`, `PriorityId` as `int`; the command takes enums. Changing them changes the API and the generated client, so it needs its own issue. See "Not candidates".
- **T2. A merge endpoint already exists, unexposed.** Open. `ProjectTasksController.PatchProjectTask` and `ProjectsController.PatchProjectStage` read, merge a JSON Patch and send the update command. Neither is an MCP tool. Candidate 3 builds on the first.

**PI and teams**

- **P1. The PI id was ignored by the objective health-check tools.** **Fixed.** `GetObjectiveHealthChecks`, `GetObjectiveHealthCheck` and `CreateObjectiveHealthCheck` dispatched with the objective id alone, so a wrong PI id passed silently. The two queries and `CreatePlanningIntervalObjectiveHealthCheckCommand` now take the PI id; a mismatch is a 404 on the reads and a refusal on the create.
- **P2. `statusId` is an `int` closed set.** Open (`CreatePlanningIntervalObjectiveHealthCheckRequest.StatusId`), undocumented in the request model, so its mapping lives only in the skill. Same remedy as T1.
- **P3. An eval rationale leaned on an instruction that did not cover it.** **Fixed** by the instructions change: `pi-log-objective-health-check`'s `confirmedBefore` cites the confirmation rule, which now covers every change.

**Delivery and products**

- **D1. Garbled sentence in wayd-delivery "Moving a target".** **Fixed.** Both target moves are refused once the record is released or withdrawn (`Release` and `Version` refuse on `Done` or `Removed`).
- **D2. Environment delete is not "never refused".** **Fixed** in wayd-delivery. `DeleteDeploymentEnvironmentCommand` refuses when the environment has deployments and the caller lacks permission to delete deployments.
- **D3. "A version is announced once" was enforced on one side.** **Fixed.** `ReleasePackage.SetManifest` now takes the versions that open releases listing the package carry directly and refuses a manifest naming one, mirroring `Release.SetContents`. Withdrawn releases are left out.
- **D4. The products purge flow was incomplete.** **Fixed** in wayd-products: children are cleared first, because `Products_GetDependencies` rolls up the subtree, and dependency rows are removed through the product that has them.
- **D5. No way to find releases listing a package.** Open. `Releases_GetReleases` filters by product, status category and `containingVersionId` only; wayd-products now says so.
- **D6. "Cut but not yet shipped" returned more.** **Fixed** in wayd-delivery: the recipe passes `statusCategory: Active`.
- **D7. Unmentioned refusals.** **Fixed** in wayd-products: `Products_Retype` refuses an inactive type; `Products_Tag` refuses an inactive tag or category.
- **D8. Eval fixture used a number for a string enum.** **Fixed.** `products-whats-in-production` matched `environmentCategory: 4`; it now matches `"Production"`.

**Imports, roadmaps, story maps, users**

- **S1. `StoryMaps_AddGoal` does not create a step.** **Fixed** in wayd-story-maps and in the tool description, both copies (`StoryMapsController` and `@wayd/mcp`).
- **S2. `StoryMaps_UpdateTask` overwrites title and description.** **Fixed** in wayd-story-maps. Exposing `RenameTask` / `SetTaskDescription` as tools remains open.
- **S3. Owner ids.** **Fixed**: wayd-users and the server instructions name the story map owner as the user-id exception.
- **S4. "The integer work item ID" has no source.** Open. `LinkWorkItemRequest.WorkItemId` is an unverified `int`; work items are identified by a Guid and a key, and nothing turns a key into the value the tool takes. wayd-story-maps now says to link only a number the user supplies. The client hides linking for the same reason.
- **S5. Story map ordering, promotion and archiving.** **Fixed** in wayd-story-maps: the default swim lane holds position 0, promoted checklist items land in the default lane, and an archived map refuses every edit.
- **I1. Re-applying a preflight is allowed by design.** Not a defect. The web client asks for confirmation rather than refusing (`use-import-actions.ts`), because the earlier run may have failed for a reason since fixed, and `Imports_Apply`'s description already tells agents to check `appliedImportProcessId`. A second apply does overwrite that link, so the preflight points at the latest run only.
- **R1. `Roadmaps_GetActivities` did not return a tree.** **Fixed.** `GetRoadmapActivitiesQuery` ran `AsNoTracking()`, so EF never connected parents and children and every activity came back at the root with empty `children` — also flattening the parent picker in the roadmap forms. Reproduced and covered by `GetRoadmapActivitiesQueryHandlerTests` against SQL Server.
- **R2. `Roadmaps_GetRoadmaps` includes archived roadmaps** unless `state` is passed. **Fixed** in wayd-roadmaps: the period recipe filters to `Active`.

## Evaluation coverage

Scenarios that exercise a candidate's sequence are listed in the summary table. Sequences with no scenario, which #975 should add before measuring:

- Recommending a project health check (candidate 7).
- Changing a package manifest (candidate 5) — the scenario most likely to catch the `versionRecord` loss.
- Updating a task field other than assignees (candidate 3).
- Deleting a product (candidate 11), recording a deployment (candidate 19), building a map (candidate 20).

Scenarios no candidate touches, useful as controls: `ppm-complete-program-open-project`, `ppm-reopen-cancelled-project`, `ppm-kpi-on-track`, `pi-team-predictability`, `teams-sprint-metrics-by-code`, `teams-backlog-health-runway`, `teams-tot-work-distribution`, `products-decommission-environment`, `products-whats-in-production`, `imports-handful-of-edits`.

## Classification by skill

**F** fixed sequence (composite), **J** judgment (stays), **R** cross-tool rule (instructions or descriptions). Sections that only list tools are omitted.

| Skill | Section | Class | Candidate |
|---|---|---|---|
| wayd-ppm | Before any change | R | |
| | Common patterns: ids and keys, people are employee ids, delivery leadership | R | |
| | Activity history | R | |
| | Someone's work | J | 12 |
| | Creating a task | F (lookups) | Not a candidate: enum codes |
| | Updating a task | F | 3 |
| | Updating a record (checklist) | F | 1, 2 |
| | Changing status, prerequisites, side effects | R | |
| | Moving a project backwards | J | |
| health-checks.md | Tools and fields, read/write shapes | R | |
| | Log, correct, or delete | J | |
| | Recommending, steps 1–6 | F | 7 |
| | Steps 7–8, classification, presenting | J | |
| kpis.md | Reading a KPI | R | |
| | Recording a measurement | F (small) | Not a candidate |
| scoring.md | Traps | R | |
| wayd-pi | Health-check recipe step 1 | F | 8 |
| | Recipe steps 2–3 | J | |
| | PI health report step 1 | F | 9 |
| | Report steps 2–3, predictability, objectives, finding PIs | J | |
| | Identifiers, metrics units, activity, risks, logging notes | R | |
| wayd-teams | Identifiers, activity, forecasts | R | |
| | Backlog health, sprint and cadence metrics, work distribution | J | |
| wayd-delivery | Four records, announce checklist, withdraw and revert | J | |
| | Overwrite, not patch | F | 4, 5 |
| | Recording what shipped steps 1–2, 7 | F | 18, 19 |
| | Where did this version go | F | 10 |
| | Ending vs deleting, announced once, instants, deployments, permissions | R | |
| wayd-products | Single-value axis | F | 17 |
| | Deleting a product: four checks | F | 11 |
| | Purge, dependencies, environments, catalog | J | |
| | Guarded changes, configuration, measures | R | |
| wayd-imports | Flow steps 5–6, finding an import | F | 13 |
| | Flow steps 3, 7, 8; atomicity; dependency order; acting on a run | J | |
| | Preflight first, file rules, limits | R | |
| wayd-roadmaps | Planned for a period, steps 2–4 | F | 14 |
| | Identifiers | F | 15 |
| | Item types, trees, visibility | R | |
| wayd-story-maps | Persona sets replace | F | 6 |
| | Destructive operations steps 1–2, 5 | F | 16 |
| | Building a map | F | 20 |
| | Navigating, analyzing | J | |
| | Ids, mutation shape, archived maps, ordering | R | |
| wayd-users | Resolve a name, reverse lookup | F | 12 |
| | No employee, inactive, several matches, fallback | J | |
| | User id versus employee id | R | |
