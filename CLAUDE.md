# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Wayd is an intelligent delivery management platform designed to give engineering leaders and teams end-to-end visibility into software delivery. It acts as a unified hub that synchronizes data from multiple business systems and combines it with capabilities those systems lack — connecting the dots so teams can see the full picture in one place. Built with Clean Architecture, Domain-Driven Design, and a modular monolith approach with a shared database.

**For domain context** (entities, relationships, business rules): See [AGENTS.md](AGENTS.md) and [docs/llms-full.txt](docs/llms-full.txt)
**For domain terminology**: See [docs/ai/domain-glossary.mdx](docs/ai/domain-glossary.mdx)
**For user-facing documentation**: See [docs/](docs/) (shared by Docusaurus and Next.js in-app docs)

Documentation site: <https://wayd.dev>

## Build and Test Commands

### .NET Backend

```bash
# Build the entire solution
dotnet build Wayd.slnx

# Build a specific project
dotnet build "Wayd.Web/src/Wayd.Web.Api/Wayd.Web.Api.csproj"

# Run all tests
dotnet test Wayd.slnx

# Run tests for a specific project
dotnet test "Wayd.Services/Wayd.Work/tests/Wayd.Work.Application.Tests/Wayd.Work.Application.Tests.csproj"

# Run specific test class or method
dotnet test --filter "FullyQualifiedName~ProjectServiceTests"

# Run architecture tests (enforce Clean Architecture rules)
dotnet test Wayd.ArchitectureTests/Wayd.ArchitectureTests.csproj

# Run the API locally
cd Wayd.Web/src/Wayd.Web.Api && dotnet run

# Database migrations (from repository root)
dotnet ef migrations add <MigrationName> --project "Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL" --startup-project "Wayd.Web/src/Wayd.Web.Api"
dotnet ef database update --project "Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL" --startup-project "Wayd.Web/src/Wayd.Web.Api"
```

### Frontend (Next.js)

From the `Wayd.Web/src/wayd.web.reactclient` directory:

```bash
npm install     # Install dependencies
npm run dev     # Run development server (with Turbopack)
npm run build   # Build for production
npm run lint    # Run linter
npm test        # Run tests
```

### .NET Aspire (Recommended for Local Development)

```bash
cd Wayd.AppHost && dotnet run
```

- Aspire Dashboard: <http://localhost:15888>
- Client: <http://localhost:3000>
- API: Dynamic HTTPS port (shown in Aspire dashboard)

### Docker

```bash
docker compose up       # Run entire stack
docker compose down     # Tear down
```

- API: <https://localhost:5001> (Swagger: <https://localhost:5001/swagger>)
- Client: <http://localhost:5002>

## Architecture

### Clean Architecture Layers

```
Domain (innermost, zero dependencies)
  ↑
Application (depends on Domain only)
  ↑
Infrastructure (depends on Application & Domain)
  ↑
Web API (depends on all layers)
```

Architecture tests in `Wayd.ArchitectureTests` enforce these dependency rules.

### Where Code Lives

- **Domain logic**: `Wayd.Services/{ServiceName}/src/{ServiceName}.Domain/Models/`
- **Commands/Queries**: `Wayd.Services/{ServiceName}/src/{ServiceName}.Application/{Feature}/Commands/` and `Queries/`
- **API endpoints**: `Wayd.Web/src/Wayd.Web.Api/Controllers/{DomainArea}/`
- **Infrastructure**: `Wayd.Infrastructure/src/Wayd.Infrastructure/{Concern}/`
- **Integrations**: `Wayd.Integrations/src/Wayd.Integrations.{SystemName}/`
- **Frontend pages**: `Wayd.Web/src/wayd.web.reactclient/src/app/`
- **Tests**: Mirror the source structure in `tests/` folders

### Key Patterns

- **CQRS with Wolverine** — All operations are commands/queries dispatched through `IDispatcher`. Controllers are thin.
- **Result Pattern** — Handlers return `Result<T>` from CSharpFunctionalExtensions. No exceptions for business logic.
- **No Repository Pattern** — EF Core DbContext used directly in handlers.
- **Vertical Slices** — Each service: `{ServiceName}.Domain/` + `{ServiceName}.Application/`
- **Feature Folders** — Application layer organized by aggregate root: `{Feature}/Commands/`, `{Feature}/Queries/`, `{Feature}/Dtos/`

## Coding Conventions

### .NET Backend

- **Time handling**: NodaTime (`Instant`, `LocalDate`). Never use `DateTime.UtcNow` — always inject `IDateTimeProvider`.
- **Async naming**: Do NOT use `Async` suffix for new async methods.
- **Validation**: FluentValidation, run by the Wolverine handler pipeline (`WolverineFx.FluentValidation`).
- **Mapping**: Mapster for DTOs.
- **Entity configuration**: Fluent API in `IEntityTypeConfiguration<T>` classes. No data annotations.
- **Package management**: Central Package Management via `Directory.Packages.props`.

### Frontend (React/Next.js)

- **API calls**: Always use NSwag-generated typed client (e.g., `getProjectsClient()`). Never use `authenticatedFetch()` directly. Clients in `wayd.web.reactclient/src/services/clients.ts`.
- **Theming**: Ant Design theme tokens only — never hardcode colors. Prefer CSS variables (`var(--ant-color-primary)`) in CSS modules over `theme.useToken()` in JS. Only use `theme.useToken()` when values are needed in JS logic.
- **State**: Redux Toolkit + RTK Query for API data. React Context for auth/theme. `useState` for local UI state.
- **PWA**: Installable via Serwist (`@serwist/turbopack`). See [Frontend Development docs](docs/contributing/frontend.mdx#pwa-progressive-web-app) for details.
- **Ant Design reference**: For component APIs, usage examples, and design tokens, fetch the machine-readable docs — per-component `https://ant.design/components/<name>.md` (e.g. `Table.md`), the full index at <https://ant.design/llms-full.txt>, and the design-token spec at <https://ant.design/design.md>. See <https://ant.design/docs/react/for-agents> for the full agent toolset (CLI + MCP server).

## Development Notes

### Authentication

Two methods, configured per deployment:

1. **Identity providers (OIDC)** — Database-managed, stored in `Identity.OidcProviders`. Supports Microsoft Entra ID and any standards-compliant OIDC provider (Google, Okta, Auth0, Keycloak). Created and managed entirely by admins via **Settings → Identity Providers** — there is no config-file or environment-variable equivalent. Frontend uses `oidc-client-ts` (PKCE redirect flow). The login page discovers configured providers at runtime from `GET /api/auth/providers`.
2. **Wayd (Local)** — JWT auth. Requires `SecuritySettings:LocalJwt:Secret` in API config.

Key files: `Wayd.Infrastructure/Auth/Local/TokenService.cs`, `Wayd.Infrastructure/Auth/Oidc/OidcTokenValidator.cs`, `wayd.web.reactclient/src/components/contexts/auth/auth-context.tsx`, `wayd.web.reactclient/src/components/contexts/auth/oidc-client-registry.ts`

User → login-provider linkage lives in a `UserIdentity` table — one active row per user, keyed by `(Provider, ProviderTenantId, ProviderSubject)`. Every authentication path resolves through the same lookup. Admins can stage tenant migrations per user; the rebind happens transactionally on the user's next sign-in from the new tenant. See [docs/contributing/configuration.mdx](docs/contributing/configuration.mdx) (Identity model + Tenant migration sections) for schema, invariants, and admin workflow.

### Authorization

Most of the app authorizes on the permission claim alone — `[MustHavePermission(action, resource)]` on the controller, producing `Permissions.{Resource}.{Action}`.

**PPM is the exception: a permission claim alone cannot change a record.** Mutating a project, program, or portfolio also requires **delivery leadership** — Owner or Manager on the record or on an ancestor (project ← program ← portfolio, inheriting downward). Sponsors and project Members are excluded. This is enforced in the domain, since only the record can answer it.

When writing or editing a PPM handler that mutates one of those aggregates:

1. Mark the command `IRequireLinkedEmployee`.
2. Inject `ICurrentPrincipal`; call `ResolvePpmActor(cancellationToken)`.
3. **Load ancestor roles in the query** — `.Include(p => p.Portfolio).ThenInclude(p => p!.Roles)`, plus `.Include(p => p.Program).ThenInclude(p => p!.Roles)` for projects.
4. Pass `actor` and `project.AncestryRoles()` into the aggregate method.

Every mutating method on these aggregates requires a `PpmActor`, so the compiler catches a missing actor. **It does not catch a missing `.Include`** — that silently empties the ancestry and denies a legitimately authorized user, and in-memory test fakes can't detect it because `.Include` is a no-op there.

`PpmActor.System` is the deliberate bypass for importers and creation paths (`grep PpmActor.System` audits every one). `Permissions.ProjectPortfolioManagement.Administer` grants domain-wide leadership but never substitutes for the permission claim.

The read side mirrors the rule as `canManageProject` / `canManageProgram` / `canManagePortfolio` DTO fields; the UI gates on permission **and** flag. Those projections and the aggregate predicates must stay in agreement.

See [docs/contributing/architecture.mdx](docs/contributing/architecture.mdx#permission-based-vs-membership-based-authorization) and [docs/user-guide/settings/permissions.mdx](docs/user-guide/settings/permissions.mdx).

### Feature Flags

Microsoft.FeatureManagement — defined in code, stored in database, managed via Settings UI.

1. Define in `Wayd.Common.Domain/FeatureManagement/FeatureFlags.cs`
2. Gate backend: `[FeatureGate(FeatureFlags.Names.MyFlag)]` or `IFeatureManager.IsEnabledAsync()`
3. Gate frontend: `requireFeatureFlag` HOC or `useFeatureFlag` hook
4. Gate menus: pass flag state into menu builder functions
5. Deploy — seeder creates flag as disabled; admin enables via UI

### OpenAPI Client Generation

NSwag generates TypeScript client from API's OpenAPI spec on Debug build. Config in `nswag.json`. Generated client in `wayd.web.reactclient/src/services/wayd-api.ts`.

### Wolverine Handler Codegen (generated, not committed)

The Wolverine handler tree at `Wayd.Web.Api/Internal/Generated/WolverineHandlers/` is **git-ignored and generated by `codegen write`, never committed**. The codegen mode is chosen by the `Wolverine:CodegenMode` config key (see `WolverineConfiguration.ResolveTypeLoadMode`):

- **Local dev → `Auto`** (`appsettings.Development.json` sets `Wolverine:CodegenMode = Auto`). Wolverine compiles handlers at runtime via `WolverineFx.RuntimeCompilation`, so a plain `dotnet run`/build needs no pre-generated tree and no codegen step. This is why there is no longer a `RegenerateWolverineHandlers` post-build target (it used to boot the app on every Debug build).
- **CI-tested builds and every published artifact → `Static`** (the default when the key is absent). Loads the pre-generated tree with no runtime Roslyn, for fast cold start. **Production is guarded**: a Production host resolving to anything but Static throws at boot (Roslyn in prod would silently regress cold start). `WolverineFx.RuntimeCompilation` is referenced so Auto works in dev/tests but is never invoked on the Static path.

How the tree reaches tests and the shipped image (see `.github/workflows/docker.yml` + the API `Dockerfile`):

- The `build-and-test-api` CI job generates the tree **once** (`codegen write`), uploads it as the `wolverine-handler-tree` artifact, then runs the full `dotnet test Wayd.slnx` suite. `Wayd.Web.Api.IntegrationTests` forces `CodegenMode=Static` (`WaydApiFactory`), so it boots that exact tree — validating the shipped dispatch path.
- The `build-and-push-image` job downloads the same artifact into the build context; the Dockerfile compiles it in (with a guard that fails the build if it is absent). **One generation per run ⇒ the tested tree is byte-identical to the shipped tree.**
- Codegen output is **DI-registration-order sensitive and not reproducible across environments** (even the OTLP exporter's presence reorders it), which is *why* it is generated once and shared rather than regenerated independently — and why committing it was noise (500+ churned files per handler change, which blocked Copilot review).
- The `codegen write` boot needs `ASPNETCORE_ENVIRONMENT=Development` (Auto mode + skips the prod JWT-secret guard in `AddLocalJwtAuth`), `WAYD_SKIP_DB_INIT=true`, a **placeholder** `DatabaseSettings:ConnectionString` (it never connects — the verb builds the host but never calls `app.Run()`), and empty `OTEL_EXPORTER_OTLP_ENDPOINT`.
- A broken codegen config is invisible to `dotnet build` and unit tests — only a real host boot and the `Wayd.Web.Api.IntegrationTests` dispatch suite catch it.
- Service location is disabled (`ServiceLocationPolicy.NotAllowed`): codegen constructor-inlines handler dependencies. A handler dependency whose registered implementation class is `internal` needs an `AlwaysUseServiceLocationFor<T>()` allow-list entry in `WolverineConfiguration` (or a public implementation) — otherwise `codegen write` fails with an `InvalidServiceLocationException` naming the type. `AmbientUserId` is on that list for correctness, not opaqueness (the middleware-written user id must be the same instance every consumer in the scope reads) — do not remove it.

### Database

Single shared `WaydDbContext`. Entity configs in `Wayd.Infrastructure/Persistence/Configuration/`. Migrations in `Wayd.Infrastructure.Migrators.MSSQL`. Auto-applied on startup via `app.Services.InitializeDatabases()`.

### Testing

- Test naming: `{ProjectName}.Tests`
- Domain fakers live in the domain test project they belong to (each `{ServiceName}.Domain.Tests/Data/` folder), NOT in `Wayd.Tests.Shared` — only the `PrivateConstructorFaker<T>` base lives there. Use per-property `With{Property}` builder extensions.
- One test class per system-under-test, named after its file; mark every test with `// Arrange` / `// Act` / `// Assert`
- Pass `TestContext.Current.CancellationToken` to every cancellable call (handler `Handle(...)` and EF queries) — not `CancellationToken.None`
- Fake DbContext implementations for each application area (e.g. `FakeWaydDbContext`); assert on `SaveChangesCallCount`
- Moq.AutoMock for automatic dependency mocking
- See [docs/contributing/testing.mdx](docs/contributing/testing.mdx) for the full conventions

## Important Considerations

- **Main branch**: `main` (not master)
- **Docker Compose**: Environment variable changes require full teardown and rebuild (`docker compose down` then `up`)
- **OpenTelemetry**: Configured in `Wayd.Infrastructure/src/Wayd.Infrastructure/OpenTelemetry/ConfigureServices.cs`. Frontend server-side only via `instrumentation.ts`.
