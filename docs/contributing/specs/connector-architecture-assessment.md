# Connector Architecture Assessment

## Verdict

The integration architecture is fundamentally right, and we should **not** adopt a plugin architecture. The work-sync path — a connector-neutral port (`IWorkItemSource`), keyed DI registration, per-connector descriptor builders, and a runner that never names a concrete connector — is already a correct ports-and-adapters design and is the reference shape for everything that follows. The work ahead is to **finish that pattern**: converge people sync onto it, introduce a single per-connector registration seam (`IConnectorModule`) to collapse the scattered switches, fix the silent DTO-mapping fallthrough (a live bug today), and break the connector-equals-one-category assumption before a multi-surface connector like GitHub lands.

## Scope and non-goals

- This is an assessment, not a refactor plan. No code changes accompany this document; the prioritized recommendations at the end each become their own PR.
- Third-party / customer-authored connectors are out of scope except for the "keep the door open" posture in the plugin section. They are not planned; we only ensure today's seams wouldn't need rework if that changed.
- Frontend redesign is out of scope. The form/detail registries are assessed (and largely endorsed) but not reworked here.

## Current state survey

### The two sync paths

**Work sync is the reference architecture.** The port lives in `Wayd.Integrations/src/Wayd.Integrations.Abstractions/IWorkItemSource.cs`: `Bind`/`TestConnection`/`GetSyncPlan`/`PrepareWorkspaceForItemSync`/`SyncIterations`/`SyncWorkItems`, all returning `Result` types. Implementations are registered as keyed services (`AddKeyedTransient<IWorkItemSource, AzureDevOpsWorkItemSource>(Connector.AzureDevOps)`), resolved by `WorkItemSourceFactory`, and initialized by binding a `SyncableConnectionDescriptor` whose `Configuration` is an opaque boxed object built by a per-connector `ISyncableConnectionDescriptorBuilder`. `WorkSyncRunner` orchestrates the whole cycle without ever naming a concrete connector, and connector-specific knobs travel through an opaque string dictionary (`WorkItemSyncFilters`, e.g. `"azdo.teamSettings"`). This is textbook hexagonal architecture, in-process.

**People sync is the legacy shape.** `PeopleSyncRunner` (`Wayd.Services/Wayd.AppIntegration/src/Wayd.AppIntegration.Application/Connections/Managers/PeopleSyncRunner.cs`) injects both concrete sources (`IEntraEmployeeSource`, `IWorkdayEmployeeSource`) directly and dispatches via three connector switches:

- `FetchEmployees` (lines 265–272) — routes to `FetchFromEntra` / `FetchFromWorkday`, each of which inlines its own entity load and credential construction
- `SourceSupportsIncremental` (lines 287–291) — hardcodes which connectors can do delta fetches
- `ResolveMatchProperty` (lines 297–312) — per-connector entity load just to read `Configuration.MatchBy`

Every new people connector grows all three switches plus a fetch method. The comment on `FetchEmployees` says it plainly: *"Add new connectors (BambooHR, ...) by adding arms here."* That instruction is the debt.

**AI providers** (AzureOpenAI, OpenAI) are connection entities with no sync surface, correctly excluded from both runners via `ConnectorCategory` gating. This must stay true — AI providers are not sync-shaped and must never be enumerated in sync runners, sync UI, or sync-trigger paths.

### Taxonomy

`Connector` and `ConnectorCategory` enums live in `Wayd.Common/src/Wayd.Common.Domain/Enums/AppIntegrations/`. `ConnectorExtensions.GetCategory()` (lines 9–17) maps each connector to exactly **one** category via a switch. Both runners and the in-memory category filter in `GetConnectionsQuery` gate on it.

### Persistence

Connections use TPH inheritance: abstract `Connection` / `Connection<TConfig>` with the `Connector` enum as discriminator, per-connector `DbSet`s on `IAppIntegrationDbContext`, secrets encrypted at rest via EF value converters, and owned collections for nested config (Azure DevOps `WorkProcesses`/`Workspaces` with `IntegrationState`). Migrations are central in `Wayd.Infrastructure.Migrators.MSSQL`.

### Shotgun-edit points when adding a connector

| # | Edit point | Debt or inherent? |
|---|---|---|
| 1 | `Connector` enum value + `GetCategory()` switch arm | **Debt** — module seam absorbs the switch |
| 2 | `GetConnectionsQuery` / `GetConnectionQuery` polymorphic adapt switches | **Debt** — and a missing arm fails *silently* (see below) |
| 3 | `ConnectionsController` pattern-match switches (Create/Update/Get-masking/Delete/Init) | **Inherent variance**, but unguarded — needs an architecture test, not erasure |
| 4 | `PeopleSyncRunner` three switches (people connectors only) | **Debt** — port unification removes them |
| 5 | Manual DI block in `Wayd.Infrastructure/src/Wayd.Infrastructure/ConfigureServices.cs` (lines 87–143) | **Debt** — modules self-register |
| 6 | EF discriminator mapping + entity config + migration | **Inherent** — persistence stays central by design |
| 7 | New `DbSet` on `IAppIntegrationDbContext` | **Inherent** |
| 8 | Frontend: `ConnectorType` enum, `CONNECTOR_NAMES`, form registry, detail registry, `getDiscriminator()` | **Mostly inherent** — forms are genuinely bespoke; the registries are exhaustively typed (compile error when missed) |
| 9 | Per-connector commands/requests (Create/Update/Delete + specials like Workday's init probe) | **Inherent** — connectors genuinely differ; genericizing would hurt |

### The silent fallthrough is a live bug, not a hypothetical

`OpenAIConnection` exists as a domain entity (`Wayd.AppIntegration.Domain/Models/OpenAI/OpenAIConnection.cs`) with EF configuration, but neither query handler has an arm for it:

- `GetConnectionsQuery.cs` lines 42–49 — no `OpenAIConnection` case; falls through to base `ConnectionListDto`
- `GetConnectionQuery.cs` lines 34–42 — line 40 is literally `// case OpenAIConnection:`, commented out

An OpenAI connection today serializes as the base DTO: no `Configuration`, no `$type` discriminator the frontend detail registry can route on — the UI sees every field as null. The handler's own comment acknowledges the failure mode. This is the cheapest, highest-certainty fix in this document.

> **Resolved (June 2026):** the OpenAI connector was removed entirely rather than mapped — it had no integration project, no Create/Update commands, no API path, and its UI tile was hidden, so no row could ever exist. The query handlers' default arms now throw instead of mapping to the base DTO, and `GetConnectionsQueryHandlerTests` / `GetConnectionQueryHandlerTests` reflection-guard that every concrete `Connection` subclass maps to a derived DTO. Enum value `2` is retired and must not be reused.

## Should we move to a plugin architecture? No.

A plugin architecture (dynamic assembly loading, `AssemblyLoadContext`/MEF, or out-of-process connector hosts) buys exactly two things: **independent deployment cadence** and **third-party authorship**. Wayd has neither need. All planned connectors — more work sync (Jira, GitHub), more people sync, new categories (source control, CI/CD, incidents), more AI providers — are first-party, compiled in-tree, and released atomically with the monolith. The roadmap is a *volume* problem; plugins solve a *trust-and-cadence* problem we don't have.

The honest cost sheet, in descending severity:

| Cost | Why it bites |
|---|---|
| **EF TPH coupling** | Each connector contributes a `Connection<TConfig>` TPH subtype, a `DbSet`, encrypted value converters, owned collections, and a central migration. A runtime plugin can contribute none of that. Supporting plugins means a parallel persistence path (JSON-blob config keyed by connector string) — losing typed owned-collection modeling, queryability, and migration safety — maintained forever alongside the typed path. This alone disqualifies plugins. |
| **Contract versioning** | `Wayd.Integrations.Abstractions` would become a published, semver'd contract. Today we refactor `IWorkItemSource` freely in one PR; with plugins every signature change is a deprecation negotiation. |
| **NSwag + typed forms** | Connection DTOs use `[JsonDerivedType]` polymorphism baked into the generated TypeScript client, and connector forms are hand-built React components in a compile-time registry. A runtime plugin can add neither. The escape hatch — JSON-schema-driven dynamic forms — is a large project that produces worse UX than the bespoke forms we have. |
| **Security / isolation** | In-proc plugins are full-trust code running inside Hangfire jobs with the shared DbContext and the secret-decryption keys. Out-of-proc fixes that at the cost of an IPC layer, auth between processes, and deployment infrastructure. |

### "Maybe someday" — the keep-the-door-open posture

External authorship is not planned but not forever ruled out. The complete (and nearly free) answer to "maybe someday":

1. **Keep `Wayd.Integrations.Abstractions` pure** — ports, descriptors, and result shapes only; no references into Wayd domain internals. That assembly *is* the future plugin contract. Enforce with an architecture test in `Wayd.ArchitectureTests`.
2. **Keep config opaque at the port boundary** — the boxed `Configuration` on `SyncableConnectionDescriptor` and the opaque filter dictionary already do this. A plugin host could later feed JSON-deserialized config through the same seam unchanged.
3. **Adopt the connector module seam** (below). A single class per connector declaring identity, categories, capabilities, and DI registrations is exactly the shape of a plugin entry point. If plugins ever become real, the module interface becomes the manifest and only *discovery* changes (assembly scan → load context or remote shim).

Explicitly do **not** build now: plugin folders, contract NuGet packaging, schema-driven dynamic forms, or `AssemblyLoadContext` infrastructure. All of it is speculative and all of it carries permanent cost.

## Target shape: finish the ports-and-adapters pattern

Four moves, ordered by the section that follows.

### 1. Unify people sync onto the keyed-DI port pattern

Create an `IEmployeeSource` port in `Wayd.Integrations.Abstractions`, mirroring `IWorkItemSource`:

- `Connector` property, `Bind(descriptor)`, `TestConnection`, and `GetEmployees(Instant? since, CancellationToken)` returning employees plus optional exclusion metadata (Workday's exclusion counts generalize as a provenance list — the runner already models this via its private `FetchEmployeesResult`).
- Per-connector descriptor builders (Entra, Workday) absorb the inline entity loads from `FetchFromEntra`/`FetchFromWorkday`. **`MatchBy` moves into the descriptor** — both configs already carry `Configuration.MatchBy`, so this is normalization, not invention; it deletes `ResolveMatchProperty` outright.
- `SupportsIncremental` becomes declared capability data (section on capabilities below), deleting the second switch.
- Keyed DI + a factory mirroring `IWorkItemSourceFactory`. Do **not** build a generic `ISourceFactory<TPort>` speculatively — extract it when the second port actually exists, if the duplication warrants it.

Do this **before the next people-sync connector**, or every switch grows another arm and the migration cost rises.

> **Resolved (June 2026):** implemented as specified, with one refinement — `MatchBy` landed on the **port** rather than the descriptor (`IEmployeeSource.MatchBy`, valid after `Bind`), since the source already casts the typed configuration and can expose it directly; same for `SupportsIncremental`. The port is `IEmployeeSource` (Bind / `GetEmployees(Instant? since, ct)` → `EmployeeFetchResult` with a connector-neutral `EmployeeExclusionCount` list); `EntraEmployeeSource` / `WorkdayEmployeeSource` adapters wrap the existing low-level `IEntraEmployeeSource` / `IWorkdayEmployeeSource` services (mirroring how `AzureDevOpsWorkItemSource` wraps `IAzureDevOpsService`); Entra and Workday descriptor builders share `ISyncableConnectionDescriptorBuilder` with the work-sync path. All three `PeopleSyncRunner` switches and its inline entity loads are gone — the runner never names a concrete connector. `PeopleSyncRunnerTests` was created alongside (it didn't exist).

### 2. The `IConnectorModule` seam

One class per connector declaring: its `Connector` enum value, its **category set**, its `ConnectorCapabilities`, and a `Register(IServiceCollection)` method that wires its keyed sources, descriptor builders, and HTTP clients. Modules are discovered by assembly scan over the known first-party assemblies at startup.

This collapses three shotgun-edit points into one file per connector:

- the `GetCategory()` switch in `ConnectorExtensions.cs`
- the manual integrations DI block in `ConfigureServices.cs` (lines 87–143)
- (eventually) the DTO-mapping switches, via a mapping registry contributed per module

**Scope discipline:** EF entity configuration, `DbSet`s, and migrations stay central. The module declares *behavior wiring*, not persistence — that boundary is what keeps the TPH model's benefits while still consolidating everything else. An architecture test asserts every `Connector` enum value has exactly one module.

### 3. Fix the silent DTO fallthrough

Three layers, strongest last:

1. **Interim one-liner:** make the `_ =>` arms in `GetConnectionsQuery`/`GetConnectionQuery` throw instead of adapting to the base DTO. A loud failure beats silent data loss; it also immediately surfaces the existing `OpenAIConnection` gap.
2. **Architecture test:** reflection over non-abstract `Connection` subclasses asserting each has a registered DTO mapping and a controller arm. This turns the whole bug class into a CI failure — including the `ConnectionsController` switches, which are otherwise hard to registry-fy because each verb takes genuinely different request types.
3. **Mapping registry via the module seam:** `Connector → Func<Connection, ConnectionListDto>` contributed by each module, replacing the switches entirely.

### 4. Multi-category taxonomy — fix before GitHub

`GetCategory()` returns a single category per connector, and everything gates on that 1:1 assumption. It breaks on the first multi-surface connector: GitHub spans work sync **and** source control (and plausibly CI/CD); Jira touches incidents. Change the model to `Connector → IReadOnlySet<ConnectorCategory>` — or better, derive it: a connector *is in* a category iff its module registers that category's source port. Runner gating becomes "has a source for my category" instead of `GetCategory() == mine`.

This forces a product decision worth recording now: **one connection serves multiple categories** (recommended — it matches how customers think about "our GitHub org": one PAT, several surfaces), rather than one connection per category per connector. Deciding this after a connector ships split across two connection rows is an expensive data migration; deciding it now is free.

> **Resolved (June 2026):** the multi-valued model landed and was then renamed to match what it actually is — capabilities. `ConnectorCategory` no longer exists: the backend declares `ConnectorCapability` (each member carrying its display category via `Display(GroupName = ...)`), gates with `Connector.GetCapabilities()` / `HasCapability()`, and DTOs expose a `Capabilities` array of `{ id, name, category }`. The primary/single category field was dropped entirely. The product decision is recorded as **one connection serves multiple capabilities**.

## Anatomy of a sync capability

Source control, CI/CD, and incidents are coming. Each new capability follows the work-sync template — this checklist is the contract for those PRs:

1. **A port** in `Wayd.Integrations.Abstractions` — the capability's `IWorkItemSource` analog: `Connector` property, `Bind(descriptor)`, `TestConnection`, plus capability-specific sync methods returning `Result` types.
2. **A capability runner** in `Wayd.AppIntegration.Application` — load active connections with the capability → build descriptor → resolve source via factory → execute sync → persist `SyncRun`.
3. **Descriptor builders** for each participating connector.
4. **Keyed DI + factory registrations**, contributed by each connector's module.
5. **A `ConnectorCapability` value** with its `Display(GroupName = ...)` category, and a typed SyncRun details record.
6. **A Hangfire job** entry in `JobManager`.

Two shared-infrastructure decisions ride along with the first new capability:

- **Make `SyncRun` capability-neutral.** The schema is work-item-flavored today and `PeopleSyncRunner` already works around it by stuffing metrics into `DetailsJson`. Formalize typed-details-JSON as the model: neutral columns (status, timestamps, trigger, error) plus a typed JSON details payload per capability.
- **No mega-`ISyncSource`.** Resist one interface spanning work items, commits, pipelines, and incidents — it would degenerate into capability-checking soup. **Ports are capability-shaped; adapters are connector-shaped.** A connector supporting three capabilities implements three ports; that's the design working, not duplication.

## What we deliberately keep

Recorded so future cleanup instincts don't erase healthy patterns:

- **Per-connector commands/requests with `$type` polymorphism.** Workday has an init probe; Azure DevOps has workspace/team mapping; Entra has neither. Genericizing create/update into property bags would trade compile-time validation, per-type FluentValidation, and NSwag-generated typed clients for stringly-typed mush. The controller switches are annoying but *honest*; guard them with the architecture test, don't erase them.
- **TPH + enum discriminator.** Typed configs, at-rest encryption via value converters, owned collections, single-table querying — all working. TPT buys nothing; JSON-blob config loses owned-collection modeling and queryability. Revisit only if plugins become real, and then as a parallel path for the plugin tier, not a migration.
- **Explicit frontend registries.** `CONNECTOR_FORM_REGISTRY: Record<ConnectorType, ComponentType>` is exhaustively typed — a missing entry is a TypeScript **compile error**. The frontend registries are the best version of this pattern in the codebase; the backend switches should envy them, not the reverse. (Worth verifying `detail-registry.tsx` and `CONNECTOR_NAMES` use the same exhaustive `Record` shape; if any are partial, making them exhaustive is the only frontend change worth doing.)
- **The opaque filter dictionary** at the port boundary (`"azdo.teamSettings"`). Textbook hexagonal: connector-specific knobs pass through the neutral runner untouched.
- **Single shared DbContext and central migrations.** Per-module contexts are a separate modular-monolith debate, not a connector-scaling problem.
- **The single-active people-sync constraint.** A domain rule (two HR sources of truth would fight over employee activation), not architectural debt. Keep the defense-in-depth check in the runner.
- **Per-connector special commands** (init probes, workspace refresh). Inherent variance — shotgun-edit point #9 is not debt.

## Capability model

> **Revised (June 2026):** an earlier draft of this section proposed a flat `ConnectorCapabilities` boolean record as the domain concept. That shape was rejected — a boolean beside a port is a parallel claim nothing verifies, and it gives integration projects no contract. The model below replaces it.

Capabilities have **three layers**, and keeping them distinct is what makes the model work:

### 1. Supported capabilities — connector-level, static, expressed as ports

What an integration *can* do is the set of ports it registers — never a flag beside them. "Azure DevOps supports work sync" **is** the keyed `IWorkItemSource` registration. When the AzDO integration later adds Repos and Pipelines, that means it ships `IRepoSource` and `IPipelineSource` implementations; the new capabilities exist because the code exists, and cannot be claimed without it.

Behavior modifiers live **on the port they modify**, not in a side-channel record:

- `IEmployeeSource.SupportsIncremental` (or a `GetEmployees(Instant? since)` design where the result declares whether it was differential) — replaces the `SourceSupportsIncremental` switch in `PeopleSyncRunner`
- "Requires init probe" = "registers an `IConnectionInitializer`" — the existing `IWorkdayConnectionInitializer`, generalized to a keyed optional port; the controller's Workday special-case becomes "if an initializer is registered, call it"

The integration author implements exactly one thing — the port — and the contract self-describes. No drift is possible between what's declared and what's implemented.

### 2. Enabled capabilities — connection-level, admin-configured

Supported is not the same as *responsible for*. When a connector supports multiple surfaces (AzDO with work + repos + pipelines; GitHub with work sync + source control), the admin configuring a connection chooses which surfaces that connection is responsible for — they may enable only work sync even though the integration supports all three.

- Runner gate becomes: **supported ∧ enabled ∧ active** (today it's category ∧ active; with one surface per connector, enabled ≡ `IsActive`).
- Single-active rules (PeopleSync, AiProvider) apply per **enabled** capability, not per connector.
- The in-category precedent already exists: AzDO workspaces carry `IntegrationState` — "sync only what the admin selected." Enabled capabilities generalize that idea up one level.
- **Build the enablement storage with the first multi-surface connector, not before.** While every connector has exactly one surface, per-connection toggles are dead weight; the decision is recorded here so the first multi-surface PR (AzDO Repos/Pipelines or GitHub) ships it.

### 3. Categories — a display projection, never a gate

Categories are *groupings of capabilities*, not a parallel taxonomy: "Work Management" is the category that groups WorkItems (and a future Repos/Pipelines); "People" groups People. Capabilities are named for the **resource or service provided** (WorkItems, People, AiProvider), not the mechanism — "sync" stays in the runner/job layer where it's accurate. Implemented (June 2026) as the `Display(GroupName = ...)` attribute on each `ConnectorCapability` enum member — there is no backend category enum at all. The wire carries each capability as `{ id, name, category }` (`ConnectorCapabilityDto`), and the settings UI groups the connector picker by those category strings (ordering and section descriptions live in the frontend's display constants). The `GetCapabilities()` switch in `ConnectorExtensions` is the interim declaration that stands in until every capability has a real port — at that point the switch body becomes a derivation and disappears as an edit point.

### The wire format is the only place booleans belong

The frontend can't probe DI, so `GetConnectorCapabilitiesQuery` projects "which ports and initializers are registered (and, later, which are enabled on this connection)" into a serializable DTO. Booleans/arrays there are fine — a wire DTO is a read model, not a source of truth. Frontend affordances (show/hide incremental sync, init step, per-surface toggles) key off this projection; the form registries stay bespoke.

**Anti-pattern warnings:** no domain-level capability record (the ports are the record); no capability marker interfaces without members; no behavior on wire DTOs.

## Prioritized recommendations

| # | Recommendation | Effort | When |
|---|---|---|---|
| 1 | ✅ **Done (June 2026)** — Fix silent DTO fallthrough: throw on default arm + reflection-guard tests over all concrete Connection subclasses. The OpenAI gap was closed by removing the never-functional OpenAI connector entirely. | Hours | ~~Before next connector~~ |
| 2 | ✅ **Done (June 2026)** — Multi-surface taxonomy, landed as **capabilities**: `Connector.GetCapabilities()` / `HasCapability()` gate everything; `ConnectorCapability` members declare their display category via `Display(GroupName)`; DTOs expose `Capabilities`; one connection serves multiple capabilities. | Hours–1 day | ~~Before GitHub specifically~~ |
| 3 | ✅ **Done (June 2026)** — People sync unified onto the keyed-DI port: `IEmployeeSource` in Abstractions (with `SupportsIncremental` and `MatchBy` declared on the port), Entra/Workday adapters + descriptor builders, `EmployeeSourceFactory`; all three `PeopleSyncRunner` switches deleted; `PeopleSyncRunnerTests` created. | 2–4 days | ~~Before next people-sync connector~~ |
| 4 | `IConnectorModule` seam + enum↔module architecture test | 2–3 days | **Before next connector** (lands naturally with #3) |
| 5 | "Anatomy of a sync category" template + category-neutral `SyncRun` | Doc'd here; refactor rides with first new category PR | **With first new category** |
| 6 | Capability model: ports as supported capabilities + `GetConnectorCapabilitiesQuery` projection (see revised Capability model section); per-connection **enabled** capabilities storage | Projection: rides with #3/#4. Enablement: ~2 days | **Projection opportunistic; enablement with first multi-surface connector** |
| 7 | Generic `ISourceFactory<TPort>` | Hours | **Only when a second port exists** — never speculatively |
| 8 | Plugin enablement (contract packaging, JSON-config path, dynamic forms, load contexts) | Weeks+ | **Only if plugins become real** — #4 is the entire door; build nothing else now |

Ordering rationale: #1 eliminates a bug class at near-zero cost. #2 is a data-model decision that is cheap now and expensive retroactively. #3 and #4 stop O(switches × connectors) growth before the connector count doubles. The category template (#5) outranks perfecting existing categories in strategic value — new categories *multiply* the pattern — but its implementation cost naturally lands with the first new category's PR.

## Appendix: adding a connector, before and after

Steps to add a new sync connector **today**:

1. `Connector` enum value
2. `GetCategory()` switch arm
3. Domain entity + configuration (TPH subtype)
4. EF config + discriminator mapping + `DbSet` + migration
5. DTOs (`List`/`Details`/`Configuration`) + `[JsonDerivedType]` registrations
6. Arms in `GetConnectionsQuery` and `GetConnectionQuery` (silent if forgotten)
7. Request models + arms in ~6 `ConnectionsController` switches
8. Commands (Create/Update/Delete + specials)
9. Source implementation + descriptor builder (work sync) or runner switch arms ×3 (people sync)
10. Manual DI registration in `Wayd.Infrastructure` `ConfigureServices`
11. Frontend: `ConnectorType`, `CONNECTOR_NAMES`, form component + registry entry, detail entry + registry, `getDiscriminator()`

**After recommendations #1–#4 and #6:**

1. `Connector` enum value
2. Domain entity + configuration, EF config + migration *(inherent — persistence is central by design)*
3. DTOs + `[JsonDerivedType]` registrations *(missing mapping = CI failure, not silent data loss)*
4. Request models + controller arms *(inherent variance, guarded by architecture test)*
5. Commands *(inherent)*
6. Source implementation(s) + descriptor builder — one per category the connector serves
7. **One `ConnectorModule`** — categories, capabilities, and all DI in a single file
8. Frontend: form component + the exhaustively-typed registry entries *(compile error if forgotten)*

Everything that remains is either genuinely connector-specific work or guarded by a compile error / CI failure. That — not a plugin runtime — is what "ready for a number of new connectors" looks like.
