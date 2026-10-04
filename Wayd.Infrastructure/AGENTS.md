# AGENTS.md — Infrastructure

The root [AGENTS.md](../AGENTS.md) applies here too. This file covers persistence and authentication.

## Database

Single shared `WaydDbContext`. Entity configs in `Wayd.Infrastructure/Persistence/Configuration/`. Migrations in `Wayd.Infrastructure.Migrators.MSSQL`. Auto-applied on startup via `app.Services.InitializeDatabases()`.

**A save commits its rows together with what records them** — audit trails, activity log entries and outbox
envelopes — and dispatches only once that transaction commits. Spanning several saves goes through
`BaseDbContext.BeginUnitOfWork`, whose `CommitAsync` commits *and* dispatches; a bare
`Database.BeginTransactionAsync` commits without delivering what those saves raised, and
`TransactionScopeTests` fails the build if one appears outside the context. Because the save owns the
transaction, `EnableRetryOnFailure` cannot be turned on — a retrying strategy refuses a user transaction it
did not start, which would break every save. See
[domain-events.mdx](../docs/contributing/domain-events.mdx#what-a-save-commits).

**Wayd expects `READ_COMMITTED_SNAPSHOT`**, which Azure SQL Database enables by default and SQL Server does
not. Startup turns it on in Development and warns elsewhere. The command timeout is the context's
(`DatabaseSettings:CommandTimeoutSeconds`, default 30); an operation that legitimately runs longer raises it
for its own scope with `WithCommandTimeout`, never globally. See
[configuration.mdx](../docs/contributing/configuration.mdx#database).

**The twelve `IXxxDbContext` interfaces are views over that one context, not separate contexts.** They constrain what each module can see; they are not persistence boundaries, and they overlap by design (`IPlanningDbContext : IWaydDbContext`). Keeping that true takes two things working together, and either alone leaves it broken:

1. `AddDomainDbContexts` registers each as a **factory** (`sp => sp.GetRequiredService<WaydDbContext>()`). `AddScoped<IFoo, WaydDbContext>()` reads as an alias but is a distinct service descriptor, so it hands out a separate context per interface.
2. Each is **allow-listed for service location** in `WolverineConfiguration`, or codegen inline-constructs its own and never consults the container at all.

A factory is opaque to codegen, so the two cannot drift apart quietly: dropping an allow-list entry fails `codegen write`. `DbContextScopeSharingTests` asserts the result against the booted container, which is the only place it is observable — unit fakes and hand-wired integration tests both pass one context to every role and so assume what is being tested.

## Authentication

Two methods, configured per deployment:

1. **Identity providers (OIDC)** — Database-managed, stored in `Identity.OidcProviders`. Supports Microsoft Entra ID and any standards-compliant OIDC provider (Google, Okta, Auth0, Keycloak). Created and managed entirely by admins via **Settings → Identity Providers** — there is no config-file or environment-variable equivalent. Frontend uses `oidc-client-ts` (PKCE redirect flow). The login page discovers configured providers at runtime from `GET /api/auth/providers`.
2. **Wayd (Local)** — JWT auth. Requires `SecuritySettings:LocalJwt:Secret` in API config.

Key files: `Wayd.Infrastructure/Auth/Local/TokenService.cs`, `Wayd.Infrastructure/Auth/Oidc/OidcTokenValidator.cs`, `wayd.web.reactclient/src/components/contexts/auth/auth-context.tsx`, `wayd.web.reactclient/src/components/contexts/auth/oidc-client-registry.ts`

User → login-provider linkage lives in a `UserIdentity` table — one active row per user, keyed by `(Provider, ProviderTenantId, ProviderSubject)`. Every authentication path resolves through the same lookup. Admins can stage tenant migrations per user; the rebind happens transactionally on the user's next sign-in from the new tenant. See [docs/contributing/configuration.mdx](../docs/contributing/configuration.mdx) (Identity model + Tenant migration sections) for schema, invariants, and admin workflow.
