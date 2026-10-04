# AGENTS.md

<!-- Codex reads at most 32 KiB of AGENTS.md per path (this file + nested ones). Keep this + the largest nested file under it; pre-commit warns when they are not. -->

Guidance for AI coding agents (Claude Code, Codex, Copilot, Cursor, Gemini, and others) working in this repository.

## Overview

Wayd is an intelligent delivery management platform: a hub that synchronizes data from the business systems engineering teams already use and adds what those systems lack, so leaders and teams see delivery end to end. Built as a modular monolith with Clean Architecture, Domain-Driven Design, and a shared database.

Documentation site: <https://wayd.dev>

### Read next

1. **[docs/ai/agent-memory.md](docs/ai/agent-memory.md)** — compact repo-specific implementation lessons
2. **[docs/llms-full.txt](docs/llms-full.txt)** — comprehensive domain context (entities, relationships, business rules)
3. **[docs/ai/domain-glossary.mdx](docs/ai/domain-glossary.mdx)** — domain terminology
4. **[docs/](docs/)** — user guide (`user-guide/`), contributor docs (`contributing/`), reference (`reference/`); shared by Docusaurus and the in-app docs

Some directories carry their own `AGENTS.md` with rules that apply only there. Read it before changing files in that directory:

- [Wayd.Services/AGENTS.md](Wayd.Services/AGENTS.md) — domain events: designing, raising, versioning, consuming
- [Wayd.Infrastructure/AGENTS.md](Wayd.Infrastructure/AGENTS.md) — the database context, transactions, authentication
- [Wayd.Web/src/Wayd.Web.Api/AGENTS.md](Wayd.Web/src/Wayd.Web.Api/AGENTS.md) — OpenAPI client generation, Wolverine handler codegen, and the hosted MCP endpoint (`/mcp`)
- [Wayd.Web/src/wayd.web.reactclient/AGENTS.md](Wayd.Web/src/wayd.web.reactclient/AGENTS.md) — the client's traps, and the Next.js version in use
- [Wayd.Web/src/Wayd.Mcp/AGENTS.md](Wayd.Web/src/Wayd.Mcp/AGENTS.md) — the MCP server (`@wayd/mcp`)

## Important Considerations

- **Resetting a database — one way only.** Start Aspire (`cd Wayd.AppHost && dotnet run`) first, then run
  **Reset Database** from the `wayd-api` resource's Actions → Commands in the Aspire dashboard. That command
  resets exactly the database the API is configured with. **Never drop, recreate, restore over or delete any
  Wayd database** (`wayd`, `wayd-seed`, `wayd-test`, or any other) by other means — `sqlcmd`, `dotnet ef
  database drop`, a script — without the user explicitly confirming the named database first. A developer's
  user secrets hold several connection strings, most commented out with `//`, so reading the target out of a
  config file is how the wrong database gets dropped. If Aspire cannot run, stop and ask; do not improvise a
  replacement.
- **Main branch**: `main` (not master)
- **Branches and commits** follow [git-workflow.mdx](docs/contributing/git-workflow.mdx):
  - Branch `<type>/<issue>-<short-summary>` (`feat/964-mcp-tool-annotations`).
  - Subject `type(scope): summary (#issue)`, 72 characters at most, imperative, no full stop. Types:
    `feat` `fix` `docs` `refactor` `test` `perf` `chore` `ci` `build`. Scopes come from a fixed list in that
    doc; leave the scope out when a change spans areas.
  - Body optional: why, never narrative.
- **Git hooks**: run `git config core.hooksPath .githooks` once per clone. `pre-push` rejects a misnamed
  branch and an MCP change that has no changeset. `commit-msg` warns — without blocking — about a subject
  that breaks the convention; follow it anyway.
- **Docker Compose**: Environment variable changes require full teardown and rebuild (`docker compose down` then `up`)
- **OpenTelemetry**: Configured in `Wayd.Infrastructure/src/Wayd.Infrastructure/OpenTelemetry/ConfigureServices.cs`. Frontend server-side only via `instrumentation.ts`.

## Before You Finish

Run what covers the change before calling it done, and report what you ran:

- **.NET** — `dotnet build` on the projects you touched, then `dotnet test` on each affected test project. Changed a project reference? Run `Wayd.ArchitectureTests` too. Changed an entity configuration? Add a migration.
- **API surface** — a changed endpoint or request/response model regenerates the TypeScript client on a Debug build of `Wayd.Web.Api`; commit the regenerated `wayd-api.ts` and `specification.json`.
- **Imports** — a changed import (row class, endpoint, or what the handler accepts) also needs: `npm run generate:import-templates` in the client; a `@wayd/mcp` changeset (its import formats come from the spec); the area's `docs/user-guide/*/bulk-import.mdx`; and `Wayd.Tools.DataGeneration.Cli.Tests`, because data generation seeds through these endpoints.
- **Data generation** — a change to what an area generates, or to a domain rule seeding relies on, runs `Wayd.Tools.DataGeneration.Cli.Tests` and updates `docs/contributing/tools/data-generation.mdx`. A recipe-format change also refreshes `docs-site/static/schemas/wayd-data/recipe.schema.json` (`RecipeLibraryTests` fails until it matches).
- **Client** — `npm run typecheck`, `npm run lint`, and `npm test` in `Wayd.Web/src/wayd.web.reactclient`.
- **MCP** — `npm test` in `Wayd.Web/src/Wayd.Mcp`, plus a changeset for anything that ships.
- **Docs** — a change users can see updates its page in `docs/user-guide/`; a new or changed MCP tool updates the README and the matching `skills/*/SKILL.md`; a change to how the code is built or structured updates `docs/contributing/` or the `AGENTS.md` that covers it.

## Repository Structure

```text
Wayd/
  Wayd.Common/                    # Shared libraries and base abstractions
  Wayd.Services/                  # Vertical slice domain services
    Wayd.Work/                    # Work items, sprints, workspaces, processes, workflows
    Wayd.Organization/            # Teams, employees, memberships
    Wayd.Planning/                # PIs, sprint mappings, objectives, risks, roadmaps
    Wayd.ProductManagement/       # Product catalog, versions, releases, deployments
    Wayd.ProjectPortfolioManagement/  # Portfolios, programs, projects, tasks
    Wayd.StrategicManagement/     # Visions, strategies, themes
    Wayd.AppIntegration/          # Integration configuration
    Wayd.Links/                   # Cross-entity relationships
  Wayd.Infrastructure/            # EF Core, auth, jobs, logging
  Wayd.Integrations/              # Azure DevOps, Microsoft Graph
  Wayd.Web/
    Wayd.Web.Api/                 # ASP.NET Core Web API
    wayd.web.reactclient/         # Next.js 16 / React 19 frontend
    Wayd.Mcp/                     # MCP server (@wayd/mcp) exposing the API as agent tools
  skills/                         # Product skills published with the MCP server (for Wayd users)
  .agents/skills/                 # Skills for agents working on this repo (wayd-testing, React)
  docs/                           # Documentation (MDX, shared by Docusaurus and Next.js)
  docs-site/                      # Docusaurus config for GitHub Pages
```

## Build and Test Commands

### .NET Backend

```bash
# Restore pinned local tools (dotnet-ef) - once per clone
dotnet tool restore

# Build the entire solution
dotnet build Wayd.slnx

# Build a specific project
dotnet build "Wayd.Web/src/Wayd.Web.Api/Wayd.Web.Api.csproj"

# Run all tests (both halves; the Testcontainers suites need Docker running)
dotnet test Wayd.slnx

# Run only the tests that need no Docker — what CI's unit job runs
./.github/scripts/dotnet-test-projects.sh unit

# Run only the Testcontainers suites — what CI's integration job runs
./.github/scripts/dotnet-test-projects.sh integration

# Categories: Unit / Integration (*.IntegrationTests); Requires=Docker on Testcontainers suites
dotnet test "<project>" --filter "Category=Unit"

# Run tests for a specific project
dotnet test "Wayd.Services/Wayd.Work/tests/Wayd.Work.Application.Tests/Wayd.Work.Application.Tests.csproj"

# Filter within ONE project: under Microsoft.Testing.Platform a module that runs
# zero tests fails (exit 8), so a solution-wide filter fails every other project
dotnet test "<project>" --filter "FullyQualifiedName~ProjectServiceTests"

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
npm run lint:deprecations  # List uses of @deprecated APIs (type-aware, slower)
npm test        # Run tests
```

### MCP Server

See [Wayd.Web/src/Wayd.Mcp/AGENTS.md](Wayd.Web/src/Wayd.Mcp/AGENTS.md).

### .NET Aspire (Recommended for Local Development)

```bash
cd Wayd.AppHost && dotnet run
```

- Aspire Dashboard: <http://localhost:15888>
- Client: <http://localhost:3000>
- API: Dynamic HTTPS port (shown in Aspire dashboard)

The AppHost opens the dashboard, signed in, on startup; `WAYD_NO_LAUNCH_BROWSER=true` turns that off.

### Docker

```bash
docker compose up       # Run entire stack
docker compose down     # Tear down
```

- API: <https://localhost:5001> (Swagger: <https://localhost:5001/swagger>)
- Client: <http://localhost:5002>

## Architecture

### Clean Architecture Layers

```text
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
- **Persistence**: `WaydDbContext` and entity configs under `Wayd.Infrastructure/src/Wayd.Infrastructure/Persistence/`; migrations in `Wayd.Infrastructure.Migrators.MSSQL/`
- **Feature flags**: `Wayd.Common/src/Wayd.Common.Domain/FeatureManagement/FeatureFlags.cs`
- **Frontend pages**: `Wayd.Web/src/wayd.web.reactclient/src/app/`; API client factories in `src/services/clients.ts` (generated client: `wayd-api.ts`)
- **Tests**: Mirror the source structure in `tests/` folders; shared fakers in `Wayd.Common/tests/Wayd.Tests.Shared/`
- **Package versions**: `Directory.Packages.props`

### Key Patterns

- **CQRS with Wolverine** — All operations are commands/queries dispatched through `IDispatcher`. Controllers are thin. Handlers must be `public`: Wolverine generates code that calls them.
- **Result Pattern** — Handlers return `Result<T>` from CSharpFunctionalExtensions. No exceptions for business logic.
- **No Repository Pattern** — EF Core DbContext used directly in handlers.
- **Vertical Slices** — Each service: `{ServiceName}.Domain/` + `{ServiceName}.Application/`
- **Feature Folders** — Application layer organized by aggregate root: `{Feature}/Commands/`, `{Feature}/Queries/`, `{Feature}/Dtos/`

## Coding Conventions

### Comments (all languages)

Every comment must be valuable, never narrative. Full rules and examples:
[coding-standards.mdx](docs/contributing/coding-standards.mdx#comments).

- A comment tells the reader what the code cannot: what a type or member is for, a non-obvious
  invariant, why a surprising line must stay, the bug it prevents, an external quirk it works around.
- No history (what the code used to do, how the fix was found, alternatives rejected — git keeps that),
  no restating the line beneath, no references to a property, flag, or branch that is not in the code.
- **.NET: classes, records, interfaces, enums, methods, and properties carry `///` XML documentation**
  — it is how readers discover what a member is for. Write the summary for someone who sees the name and
  signature but not the body; never `Gets or sets the X`.
- `///` on `Wayd.Web.Api` request/response models is published: it becomes the OpenAPI `description` and
  the generated TypeScript client's JSDoc. Endpoint summaries come from `[OpenApiOperation]`.

### .NET Backend

- **Time handling**: NodaTime (`Instant`, `LocalDate`). Never use `DateTime.UtcNow` — always inject `IDateTimeProvider`. Migrations are the sole exception (no DI); there, format timestamps interpolated into `migrationBuilder.Sql(...)` with `CultureInfo.InvariantCulture` and `"yyyy-MM-ddTHH:mm:ss.fff"` — the current culture's default format is not parseable by SQL Server on non-Windows hosts (ICU 72+ emits `U+202F` before AM/PM, failing with error 241).
- **Async naming**: Do NOT use `Async` suffix for new async methods.
- **Validation**: FluentValidation, run by the Wolverine handler pipeline (`WolverineFx.FluentValidation`).
- **Mapping**: Mapster for DTOs.
- **Closed sets** (statuses, roles, categories the code branches on) cross the API as enum codes, never `int`: type the parameter as the enum, and give its lookup DTO a `Code`. User-defined values are entities referenced by id. See [Closed sets and entities](docs/contributing/api.mdx#closed-sets-and-entities).
- **Entity configuration**: Fluent API in `IEntityTypeConfiguration<T>` classes. No data annotations.
- **Package management**: Central Package Management via `Directory.Packages.props`.

### Frontend (React/Next.js)

- **API calls**: Always use NSwag-generated typed client (e.g., `getProjectsClient()`). Never use `authenticatedFetch()` directly. Clients in `wayd.web.reactclient/src/services/clients.ts`.
- **Theming**: Ant Design theme tokens only — never hardcode colors. Prefer CSS variables (`var(--ant-color-primary)`) in CSS modules over `theme.useToken()` in JS. Only use `theme.useToken()` when values are needed in JS logic.
- **Dates**: a `LocalDate` is generated as `string` (`"YYYY-MM-DD"`), an `Instant` as `Date`. Handle calendar dates only through `src/utils/calendar-date.ts` — never `new Date(x)` or `dayjs.utc(x)`, which shift the day west of UTC. See [Dates and instants](docs/contributing/frontend.mdx#dates-and-instants).
- **State**: Redux Toolkit + RTK Query for API data. React Context for auth/theme. `useState` for local UI state.
- **PWA**: Installable via Serwist (`@serwist/turbopack`). See [Frontend Development docs](docs/contributing/frontend.mdx#pwa-progressive-web-app) for details.
- **Ant Design reference**: For component APIs, usage examples, and design tokens, fetch the machine-readable docs — per-component `https://ant.design/components/<name>.md` (e.g. `Table.md`), the full index at <https://ant.design/llms-full.txt>, and the design-token spec at <https://ant.design/design.md>. See <https://ant.design/docs/react/for-agents> for the full agent toolset (CLI + MCP server).

## Development Notes

### Authentication

OIDC identity providers (database-managed, created only through **Settings → Identity Providers**) or local JWT. One active `UserIdentity` row links each user to a login provider. Details: [Wayd.Infrastructure/AGENTS.md](Wayd.Infrastructure/AGENTS.md#authentication).

### Authorization

Most of the app authorizes on the permission claim alone — `[MustHavePermission(action, resource)]` on the controller, producing `Permissions.{Resource}.{Action}`.

**PPM is the exception: a permission claim alone cannot change a record.** Mutating a project, program, or portfolio also requires **delivery leadership** — Owner or Manager on the record or on an ancestor (project ← program ← portfolio, inheriting downward). Sponsors and project Members are excluded. This is enforced in the domain, since only the record can answer it.

When writing or editing a PPM handler that mutates one of those aggregates:

1. Mark the command `IRequireLinkedEmployee`.
2. Inject `ICurrentPrincipal` and `ICurrentUser`; call `ResolvePpmActor(currentUser, cancellationToken)`. Both are needed: the actor carries the user id alongside the employee id, because records that freeze attribution (project status history) need the account as well as the employee.
3. **Load ancestor roles in the query** — `.Include(p => p.Portfolio).ThenInclude(p => p!.Roles)`, plus `.Include(p => p.Program).ThenInclude(p => p!.Roles)` for projects.
4. Pass `actor` and `project.AncestryRoles()` into the aggregate method.

Every mutating method on these aggregates requires a `PpmActor`, so the compiler catches a missing actor. **It does not catch a missing `.Include`** — that silently empties the ancestry and denies a legitimately authorized user, and in-memory test fakes can't detect it because `.Include` is a no-op there.

`PpmActor.System` is the deliberate bypass for importers and creation paths (`grep PpmActor.System` audits every one). `Permissions.ProjectPortfolioManagement.Administer` grants domain-wide leadership but never substitutes for the permission claim.

The read side mirrors the rule as `canManageProject` / `canManageProgram` / `canManagePortfolio` DTO fields; the UI gates on permission **and** flag. Those projections and the aggregate predicates must stay in agreement.

**Sprint lifecycle is membership-gated too**, but in the handler rather than the aggregate: start/complete/reopen need `Permissions.Iterations.Update` plus membership of the sprint's team or its direct team of teams (`SprintAuthorization.CanManageTeamSprints` → `IsTeamMemberQuery`), waived by `Permissions.Iterations.Administer`. The lifecycle rules themselves live in `TeamSprintTimeline`, which `GetSprintQuery` reuses for the DTO's `CanStart`/`CanComplete`/`CanReopen`.

See [docs/contributing/architecture.mdx](docs/contributing/architecture.mdx#permission-based-vs-membership-based-authorization) and [docs/user-guide/settings/permissions.mdx](docs/user-guide/settings/permissions.mdx).

### Feature Flags

Microsoft.FeatureManagement — defined in code, stored in database, managed via Settings UI.

1. Define in `Wayd.Common.Domain/FeatureManagement/FeatureFlags.cs`
2. Gate backend: `[FeatureGate(FeatureFlags.Names.MyFlag)]` or `IFeatureManager.IsEnabledAsync()`
3. Gate frontend: `requireFeatureFlag` HOC or `useFeatureFlag` hook
4. Gate menus: pass flag state into menu builder functions
5. Deploy — seeder creates flag as disabled; admin enables via UI

### System Settings

Org-wide runtime settings are typed sections (`ISettingsSection<TSelf>` records in `Wayd.Common.Domain/Settings/`),
one JSON row per section in `App.SystemSettings`. Property initializers are the defaults and a missing value
reads as its default, so adding a setting needs no migration — but **section keys and property names can never
be renamed** (a rename silently reverts the stored value). Read through `ISettings<TSection>`; write through
`ISystemSettingsStore.Save`, which runs the section's validator and records the change in the Activity log. See
[configuration.mdx](docs/contributing/configuration.mdx#system-settings).

### OpenAPI Client Generation

NSwag regenerates the TypeScript client (`wayd.web.reactclient/src/services/wayd-api.ts`) on a Debug build of the API, and that build **boots the API** — gate any new database-touching startup work behind `HostIntrospection.SkipsDatabaseInitialization`, or the build fails. After changing a CSV import row class, run `npm run generate:import-templates` in the client. Details: [Wayd.Web/src/Wayd.Web.Api/AGENTS.md](Wayd.Web/src/Wayd.Web.Api/AGENTS.md#openapi-client-generation).

### Wolverine Handler Codegen (generated, not committed)

The handler tree under `Wayd.Web.Api/Internal/Generated/WolverineHandlers/` is generated, git-ignored, and never compiled into a local build (dev runs `Auto`; CI and every published artifact run `Static`). A handler dependency whose implementation is `internal` needs an `AlwaysUseServiceLocationFor<T>()` entry in `WolverineConfiguration`; never remove the `AmbientUserId` or `IXxxDbContext` entries — they exist for correctness. Details: [Wayd.Web/src/Wayd.Web.Api/AGENTS.md](Wayd.Web/src/Wayd.Web.Api/AGENTS.md#wolverine-handler-codegen-generated-not-committed).

### Domain Events

**Read [Wayd.Services/AGENTS.md](Wayd.Services/AGENTS.md#domain-events) before adding, changing, or consuming a domain event.** The rules most often missed:

- Name an event for what happened, matching the method that raises it — never a bare `XxxUpdatedEvent`.
- Raise only when something actually changed, comparing after assignment, never against the arguments.
- An evented aggregate changes only through events: no `ExecuteUpdate`, `ExecuteDelete`, raw SQL, or data-fix migration on its table.
- A change carries both ends (`From*`/`To*`, the previous value).
- Every event passes an explicit version; a breaking change is a new `...V2` type and the old one is frozen with `[Obsolete]`.

### Database

One shared `WaydDbContext`; the twelve `IXxxDbContext` interfaces are views over it, not separate contexts. Span several saves with `BaseDbContext.BeginUnitOfWork`, never a bare `Database.BeginTransactionAsync`. Details: [Wayd.Infrastructure/AGENTS.md](Wayd.Infrastructure/AGENTS.md#database).

### Testing

- Test naming: `{ProjectName}.Tests`
- Domain fakers live in the domain test project they belong to (each `{ServiceName}.Domain.Tests/Data/` folder) — only the `PrivateConstructorFaker<T>` base lives centrally, in `Wayd.TestData.Core` (reached transitively via `Wayd.Tests.Shared`). Two deliberate exceptions: cross-cutting fakers (`FeatureFlagFaker`, `OidcProviderFaker`, `PersonalAccessTokenFaker`) live in `Wayd.Tests.Shared/Data/`, and Organization publishes its fakers from `Wayd.Organization.TestData`. Reuse those rather than duplicating them. Use per-property `With{Property}` builder extensions.
- One test class per system-under-test, named after its file; mark every test with `// Arrange` / `// Act` / `// Assert`
- Pass `TestContext.Current.CancellationToken` to every cancellable call (handler `Handle(...)` and EF queries) — not `CancellationToken.None`
- Fake DbContext implementations for each application area (e.g. `FakeWaydDbContext`); assert on `SaveChangesCallCount`
- Moq.AutoMock for automatic dependency mocking
- See [docs/contributing/testing.mdx](docs/contributing/testing.mdx) for the full conventions
