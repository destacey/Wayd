---
title: "Spec: MediatR to Wolverine Migration"
description: Investigation, decision record, and phased migration plan for replacing MediatR with Wolverine.
audience: [developer, agent]
---

# MediatR → Wolverine Migration

| | |
|---|---|
| **Status** | Approved direction — planning complete, implementation not started |
| **Date** | 2026-07-16 |
| **Decision** | Migrate from MediatR 12.5.0 to Wolverine 6.x using a facade-first, phased approach |

## Motivation

Two drivers, both required to justify the churn:

1. **MediatR is a dead end for us.** MediatR v13+ moved to a commercial license. We are pinned at 12.5.0 (Apache-2.0), which is safe to run indefinitely but receives no new features or bug fixes. Staying pinned means carrying frozen infrastructure forever.
2. **We want the in-process → messaging evolution path.** Wolverine offers a smooth progression from in-process events (what we have) to a durable transactional outbox, scheduled messages, and eventually external transports if modules are ever split out — without rewriting handler code at each step. MediatR has no equivalent path.

An in-house dispatcher (~150 LOC, viable because we own the `ICommand`/`IQuery` abstractions) was considered and rejected: it solves the license driver but rebuilds MediatR's ceiling rather than raising it.

## Current MediatR footprint (as of 2026-07-16)

| Surface | Scale |
|---|---|
| Command/query handler implementations | ~890 (`ICommandHandler` ~500, `IQueryHandler` ~390), all `internal sealed` |
| `Send()` call sites | ~1,000 across ~65 files (45 controllers, sync runners, `JobManager`, `UserService`, some handler-to-handler) |
| Active pipeline behaviors | 2 — `ValidationBehavior`, `PerformanceBehavior` (in `Wayd.Common.Application/Behaviors/`); `LoggingBehavior` and `UnhandledExceptionBehavior` exist but are not registered |
| Domain events | Published after `SaveChanges` in `BaseDbContext.SendDomainEvents()` via `EventPublisher` → `IPublisher.Publish(EventNotification<TEvent>)`; 7 concrete event handlers (cross-domain replication) |
| MediatR features NOT used | Streaming, pre/post processors, exception handlers — none |
| Registration | 9 per-module `AddMediatR` calls (one per Application assembly); behaviors registered once in `Wayd.Common.Application` |
| Test coupling | Very low — 372 direct `handler.Handle(...)` calls; only ~5 test files mock `ISender` |

Incidental findings to clean up regardless of migration:

- `Wayd.Common.Domain.csproj` references `MediatR.Contracts` but no source in that project uses it (vestigial).
- `Wayd.Web.Api/GlobalUsings.cs` has `global using MediatR;`.
- The generic entity events (`EntityCreatedEvent<T>`, `EntityUpdatedEvent<T>`, `EntityDeletedEvent<T>`, etc.) are published but have **zero subscribers** — they are published into the void today.

## Target

**Wolverine 6.x** (verified July 2026: supports .NET 9/10; core is open source; JasperFx sells support plans, not the library). Controllers stay plain MVC + NSwag — Wolverine.HTTP is not in scope.

Key packages: `WolverineFx`, `WolverineFx.FluentValidation`, and at Stage B `WolverineFx.SqlServer` + `WolverineFx.EntityFrameworkCore`.

## Migration phases

### Phase 0 — Retire the generic entity events (standalone cleanup, independent of the migration) ✅ Complete (PR #659)

The `EntityCreatedEvent<T>` / `EntityUpdatedEvent<T>` / `EntityDeletedEvent<T>` / `EntityActivatedEvent<T>` / `EntityDeactivatedEvent<T>` pattern is being removed as a pattern, regardless of the migration: the events have zero subscribers, they wrap live EF entity instances (never serializable, so incompatible with any future durable messaging), and they add log noise. Measured surface (2026-07-16):

- 66 raise sites (`Wayd.Work` 38, `Wayd.AppIntegration` 16, `Wayd.Common` 12) — delete the `AddDomainEvent(...)` calls.
- 5 event records + `IGenericDomainEvent` in `Wayd.Common.Domain/Events/` — delete.
- The `IGenericDomainEvent` special-case logging branch in `EventPublisher` — delete.
- 1 test file references them (`OidcProviderTests`) — update assertions.

When a real subscriber need appears later, define a concrete, serializable event (id + payload, like `TeamCreatedEvent`) — that is the Wolverine-compatible shape.

Doing this first shrinks the migration: Phase 2 loses a task, the "no-route log noise" question disappears, and the inline-only carve-out at Stage B is no longer needed.

### Phase 1 — Seam prep ✅ Complete (safe now, still on MediatR)

Independently valuable; reduces MediatR's blast radius to one project.

1. **Done.** Introduced the `IDispatcher` facade (injected interface — see "Open decisions" for the resolved shape and rationale) over `ISender` and migrated all ~60 injection sites (45 controllers, the sync runners, `JobManager`, `UserService`, and the handful of application-layer classes that inject a dispatcher) to it. The facade infers the response type from the marker interfaces, so all call sites stayed syntactically unchanged apart from the injected type name.
2. **Done.** Removed `global using MediatR;` from `Wayd.Web.Api` (added `global using Wayd.Common.Application.Interfaces;` so controllers resolve `IDispatcher`); removed now-unused file-level `using MediatR;` across the migrated files.
3. **Done.** Removed the vestigial `MediatR.Contracts` reference from `Wayd.Common.Domain`.

After Phase 1, `using MediatR;` appears only inside `Wayd.Common.Application` (markers + behaviors + facade impl + the `EventNotification`/`IEventNotificationHandler` wrappers) and in `EventPublisher` (the `IPublisher` path, untouched until Phase 2). The MediatR **package** references still exist per-module because the markers extend `IRequest`; that drops out in Phase 2 (see the note under "Open decisions").

### Phase 2 — The swap, at behavior parity ("Stage A", ~3–5 days + regression bake)

Events remain in-process, inline, synchronous. No outbox yet.

1. Decouple `ICommand`/`ICommand<T>`/`IQuery<T>` from `IRequest<T>` (they become pure markers; message records stay `public sealed record`).
2. Replace the 9 `AddMediatR` calls with a single `UseWolverine()` on the host builder, with explicit `Discovery.IncludeAssembly(...)` per Application assembly and discovery filters so non-CQRS `*Handler` classes are not scooped up.
3. Make handlers `public` (Wolverine's code generation cannot call internal types) and flip the `Handlers_ShouldBeInternal` architecture test.
4. Reimplement the Phase 1 facade over `IMessageBus.InvokeAsync<T>`.
5. Validation: adopt `WolverineFx.FluentValidation` with `RegistrationBehavior.ExplicitRegistration` (we keep our `AddValidatorsFromAssembly` registrations — the default discovery mode would double-register and duplicate every failure) and a custom `IFailureAction<T>` that throws our `Wayd.Common.Application.Exceptions.ValidationException`, preserving the exact `ExceptionMiddleware` HTTP contract. Fallback if the hook proves too rigid: port `ValidationBehavior` as custom middleware (~30 lines).
6. Port `PerformanceBehavior` as Wolverine middleware, keeping the `ILongRunningRequest` opt-out.
7. **Identity propagation middleware (required in this phase, not later):** stamp the current user ID into the message envelope headers on send; on execution, restore it via `ICurrentUserInitializer.SetCurrentUser` in the handler's scope. See "Identity and DI scoping" below for why.
8. Retarget `EventPublisher` from `IPublisher` to `IMessageBus.PublishAsync`, publishing the concrete event type directly. Delete the `EventNotification<TEvent>` wrapper and its `Activator.CreateInstance` reflection; event handlers implement plain `Handle(TEvent, CancellationToken)`.
9. Architecture tests: update reflection to the new shapes; **add a test asserting exactly one handler per command/query** (Wolverine silently *combines* multiple handlers for the same message — MediatR gave us this safety implicitly).
10. Register Wolverine's OpenTelemetry activity sources in the OTel configuration.
11. Codegen workflow: `TypeLoadMode.Auto` in dev; pre-generated types + `AssertWolverineConfigurationIsValid` startup assertion in CI/prod.
12. Delete MediatR packages.

### Stage B — Durable outbox (~1–2 days after Phase 2 is stable)

1. Add `PersistMessagesWithSqlServer(...)` and `AddDbContextWithWolverineIntegration<WaydDbContext>()`.
2. Rework `BaseDbContext.SendDomainEvents()`: instead of publishing **after** `SaveChanges` returns (today a crash between commit and publish silently drops events), enlist events with the Wolverine-integrated `IMessageBus` **during the transaction** so envelopes commit atomically with entity changes, and delivery happens post-commit via Wolverine's durability agent.
3. Wolverine's envelope tables are provisioned by Weasel at startup, parallel to (not inside) our EF migrations. Pick a dedicated schema (do not land in `dbo`). Testcontainers integration tests will auto-provision.
4. Configure System.Text.Json NodaTime serialization for durable event payloads.

### Stage C — Selective async routing (incremental, ongoing)

Move handlers from inline to durable local queues **per event type**, not globally:

- **Must stay inline:** cross-domain replication events (same-Id projections) that in-request reads or subsequent commands depend on. Going async breaks read-your-writes for those flows. The replication map is the input to this classification.
- **Go durable/async first:** genuinely fire-and-forget handlers — audit trails, notifications, search-index updates.

Once queued, failures no longer reach `ExceptionMiddleware` — they are governed by Wolverine **failure policies** (retry/backoff, requeue, dead-letter queue). This is a new design surface with no MediatR equivalent; budget a deliberate design session rather than accepting defaults. Reconcile with the Hangfire `[AutomaticRetry]` semantics if/when job consolidation happens (out of scope for this spec).

### Phase 4 — Documentation

Update: `CLAUDE.md`, `AGENTS.md`, `docs/contributing/architecture.mdx`, `api.mdx`, `adding-a-feature.mdx`, `docs/reference/technology-stack.mdx`, `attribution.mdx`.

## Identity and DI scoping (the biggest behavioral difference)

MediatR resolves handlers from the **caller's** DI scope. Wolverine executes every message in a **fresh scope**. Consequences:

- **HTTP → handler: unaffected.** `CurrentUser` lazily reads `IHttpContextAccessor` (AsyncLocal-backed), which flows with the async call chain regardless of scope. Inline `InvokeAsync` from a controller sees the same user; `BaseDbContext` auditing keeps working.
- **Hangfire jobs: breaks silently at Phase 2 without the identity middleware.** `SetCurrentUser` sets fields on one scope's `CurrentUser` instance; the Wolverine handler's fresh scope has a different instance and no `HttpContext` → blank user, silent audit-attribution loss. Hence the envelope-header middleware is a Phase 2 requirement.
- **Durable handlers (Stage B+):** run later, on agent threads, possibly after the originating request completed — there is fundamentally no ambient user; identity must travel on the envelope. Same middleware covers it.
- **Nested sends get isolated DbContexts.** Today nested `Send` calls share the caller's scoped `WaydDbContext`; under Wolverine each gets its own. Each command already does its own `SaveChanges`, so this is mostly safer — but audit sync-runner flows for reliance on shared change-tracker state.
- **Event handlers stop sharing the publisher's DbContext.** Today they run synchronously inside the publisher's `SaveChanges` call, in the same scope (an accident of `EventPublisher` being injected into `BaseDbContext`). Under Wolverine they get fresh contexts reading just-committed data — cleaner, but verify the 7 replication handlers against this change.

## Other trade-offs and gotchas

| Trade-off | Impact / mitigation |
|---|---|
| Handlers must be `public` | ~890 classes flip from `internal sealed` to `public sealed`; architecture test inverted. Deliberate loss of encapsulation — accepted. |
| Return values become cascaded messages under `Publish` | All handlers return `Result`/`Result<T>`. Fine via `InvokeAsync`; guard against `Publish` misuse with an architecture test or policy. |
| Multiple handlers for one message are combined, not rejected | New architecture test enforces exactly one handler per command/query. |
| Routing is by concrete type — no open-generic handler patterns | Only closed-generic usage exists today (`IntegrationStateChangedEvent<Guid>` — fine). Constrains future "one handler for all entity events" patterns. |
| Generic entity events carry live EF entities | Not serializable — can never be routed durable. Removed as a pattern in Phase 0 (subscriber-less today). |
| Validators run sequentially, not `Task.WhenAll` | Improvement: the current parallel run is a latent EF Core concurrency bug when two validators for one request share the scoped DbContext. |
| Validation overhead per message | Improvement: Wolverine codegen bakes validation only into pipelines of message types that have validators; MediatR resolved an (often empty) `IEnumerable<IValidator<T>>` on every Result-returning request. |
| Cold-start code generation | Affects dev inner loop, NSwag Debug-build boot, integration tests. Mitigate with `TypeLoadMode.Auto` (dev) and pre-generated types (CI/prod). |
| Debugging character changes | Stack traces route through generated types; generated code can be dumped and read. |
| Trace shape changes | Wolverine emits its own OTel activities/metrics — register sources, tune log noise. |
| Smaller ecosystem than MediatR | Accepted; commercial support available from JasperFx, which aligns with the maintenance driver. |

## What we don't lose

- No MediatR features in use beyond `Send`/`Publish` + 2 behaviors — nothing exotic to replace.
- Unit tests are almost untouched (direct `Handle` calls); only ~5 `ISender` mock sites move to the facade.
- Controllers, NSwag client generation, and the frontend are unaffected.
- Two current behaviors the migration "changes" were latent bugs it fixes: parallel validators sharing a DbContext, and events published after commit with no durability.

## Open decisions

- [x] Facade shape and name — **resolved (Phase 1 implemented):** an injected interface, `IDispatcher`, in `Wayd.Common.Application.Interfaces`, replacing every `ISender` injection site. It exposes three response-inferring overloads that mirror the marker interfaces so call sites stay identical (`_dispatcher.Send(cmd, ct)`):
  - `Task<Result> Send(ICommand command, CancellationToken ct = default)`
  - `Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)`
  - `Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)`

  The three overloads are unambiguous because `ICommand`, `ICommand<T>`, and `IQuery<T>` are disjoint hierarchies (none extends another). No MediatR types appear in the contract, so it re-implements cleanly over `IMessageBus.InvokeAsync<T>(object)` in Phase 2. The Phase 1 implementation, `MediatRDispatcher` (`internal sealed`, in `Wayd.Common.Application/Dispatching/`), is a thin pass-through to `ISender.Send` and is the only non-behavior/non-marker type in the codebase that still names an MediatR dispatch type. Chosen over `IMessageBus` extension methods because an injected interface is the minimal rename (`ISender sender` → `IDispatcher dispatcher`) and is trivially mockable — the ~5 `Mock<ISender>`/`GetMock<ISender>()` test sites became `IDispatcher` with no other change.

  Note: `ICommand`/`ICommand<T>`/`IQuery<T>` still extend `IRequest<…>` in Phase 1, so the per-module MediatR **package** references remain (the markers transitively need them); only file-level `using MediatR;` statements and the `ISender` indirection were confined. Decoupling the markers from `IRequest` — which lets the package references drop out of every project except `Wayd.Common.Application` — is Phase 2, task 1.
- [ ] Schema name for Wolverine envelope tables.
- [x] Fate of the subscriber-less generic entity events — **resolved: remove the pattern entirely (Phase 0)**.
- [ ] Failure-policy design for Stage C (retry/backoff/DLQ per exception type).
- [ ] Event classification (inline vs. durable) — requires the cross-domain replication map as input.
- [ ] Whether/when to consolidate Hangfire jobs onto Wolverine scheduling (explicitly out of scope here).

## References

- [Wolverine documentation](https://wolverinefx.net/)
- [Handler discovery and public-type requirement](https://wolverinefx.net/guide/handlers/)
- [FluentValidation middleware](https://wolverinefx.net/guide/handlers/fluent-validation.html)
- [EF Core durable outbox integration](https://wolverinefx.net/guide/durability/efcore.html)
- [Wolverine releases](https://github.com/JasperFx/wolverine/releases)
