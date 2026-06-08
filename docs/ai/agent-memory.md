# Agent Memory

This file captures compact repo-specific lessons that future coding agents should read early. Keep it short and practical. Use `CLAUDE.md`, `AGENTS.md`, `docs/llms-full.txt`, and `docs/ai/domain-glossary.mdx` for full architecture and domain context.

## Startup Checklist

1. Read root `AGENTS.md` first.
2. Read `CLAUDE.md` for build commands, architecture conventions, and generated-client rules.
3. Read this file for recent implementation lessons and repo habits.
4. For domain work, use `docs/llms-full.txt` and `docs/ai/domain-glossary.mdx`.

## Generated API Client

- Do not hand-edit `Wayd.Web/src/wayd.web.reactclient/src/services/wayd-api.ts`.
- NSwag regenerates `wayd-api.ts` and `Wayd.Web/src/Wayd.Web.Api/wwwroot/api/v1/specification.json` during a Debug build of the API project.
- Useful command: `dotnet build "Wayd.Web/src/Wayd.Web.Api/Wayd.Web.Api.csproj"`.
- Frontend RTK Query slices should call generated client methods from `src/services/clients.ts`; never use `authenticatedFetch()` directly.

## Backend Patterns

- Application handlers use EF Core DbContext directly, not repositories.
- New async methods should not use an `Async` suffix.
- Use NodaTime (`Instant`, `LocalDate`) in application/domain code. Convert API `DateTime` inputs at the controller boundary.
- Controllers should stay thin and delegate to MediatR commands/queries.
- Business validation should use `Result<T>` and FluentValidation where appropriate, not business exceptions.

## Frontend Patterns

- Use RTK Query for API data access and cache tags.
- Optional/heavy reports and grids are commonly loaded with `next/dynamic` and `ssr: false`.
- Existing details pages often open optional reports as closeable dynamic tabs from `PageActions`.
- Ant Design theme tokens and CSS variables are preferred. Do not hardcode visual colors.
- The frontend lint script runs `eslint .` even when file paths are passed, so unrelated warnings may appear.

## Testing Notes

- Domain fakers live in domain test projects and are referenced by application tests when useful. For Work items, use `Wayd.Work.Domain.Tests/Data/WorkItemFaker.cs`.
- Application test projects often include fake DbContexts, for example `FakeWorkDbContext`.
- React page tests can mock heavy Ant Design surfaces and dynamic imports when the behavior under test is page wiring rather than AntD itself.
- Next.js route `params` are promises in app-router pages. In React 19 tests, a fulfilled thenable can avoid getting stuck in Suspense for unit-level page tests.

## Cycle Time Report Rules

- Cycle time is `DoneTimestamp - ActivatedTimestamp`.
- Cycle-time report work items should remain aligned with the team report rules:
  - Requirement-tier work items only.
  - Done status category for the report query.
  - Done date range filters on `DoneTimestamp`.
  - Items without a valid `WorkItemListDto.CycleTime` are excluded by the shared frontend filtering.
- Team cycle time uses team-owned work items.
- Employee cycle time uses work items assigned to the employee via `AssignedToId`.

## Recent Feature Notes

- Portfolio project ranking added:
  - Domain: `Project.Rank` is a required fractional sort key; display `Position` is derived per portfolio in `GetProjectsQuery`.
  - API: `PUT /api/ppm/portfolios/{id}/project-ranks`, `PUT /api/ppm/portfolios/{id}/project-ranks/rebalance`, and `GET /api/ppm/portfolios/{id}/ranking-scoreboard`.
  - Frontend: portfolio details has a `Ranking` tab with `ProjectRankingBoard`; drag is disabled while sorted/filtered/searched.
  - Maintenance: `PortfolioRankRebalance` Hangfire job re-spaces dense fractional ranks.
- Employee cycle time report added:
  - Query: `GetEmployeeWorkItemsQuery` in `Wayd.Work.Application/WorkItems/Queries`.
  - Endpoint: `GET /api/organization/employees/{id}/work-items`.
  - Frontend wrapper: `EmployeeCycleTimeReport` in `components/common/work/cycle-time-report.tsx`.
  - Employee details page opens it from `Reports > Cycle Time Report`.
