# Wayd

**An intelligent delivery management platform designed to give engineering leaders and teams end-to-end visibility into software delivery.**

When delivery spans multiple teams, projects, and systems, visibility breaks down. Wayd brings it all together — tracking work items, aligning teams to planning intervals and products, and surfacing dependencies across the organization.

**Core Philosophy:** Wayd acts as a unified hub that synchronizes data from multiple business systems and combines it with capabilities those systems lack — connecting the dots so teams can see the full picture in one place.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)

## 📚 Documentation

[Full Documentation](https://wayd.dev) | [Contributing Guide](CONTRIBUTING.md) | [Developer setup](https://wayd.dev/docs/contributing/prerequisites)

## ✨ Key Features

- Organization - Teams, teams of teams, and organizational structure, with memberships tracked over time
- Planning - Planning intervals and their iterations, objectives and risks, roadmaps, story maps, and planning poker
- Work Management - Work items, sprints, and dependencies across workspaces, with configurable work processes and workflows
- Portfolio Management - Portfolios, programs, and projects, with lifecycle status and strategic alignment
- Strategic Management - Strategic themes that guide investment and prioritization
- Product Management - A product catalog with versions, releases, deployments, and delivery metrics
- External Integrations - Sync work from Azure DevOps and people from Microsoft Entra ID and Workday on automated cycles
- AI Assistants - An MCP server that lets assistants such as Claude read and manage delivery data
- User Management - Sign-in through OIDC identity providers (such as Microsoft Entra ID) or local accounts, role-based access control, and account lifecycle (activate/deactivate/unlock)
- Observability - Built-in OpenTelemetry support for tracing, metrics, and logging

## 🏗️ Technology Stack

**Backend:**

- .NET 10.0 with Clean Architecture
- ASP.NET Core Web API
- Entity Framework Core + SQL Server
- Wolverine (CQRS), FluentValidation
- Hangfire (background jobs)
- OpenTelemetry observability

**Frontend:**

- Next.js 16 (React 19, TypeScript)
- Ant Design UI components
- Redux Toolkit + RTK Query
- OIDC sign-in (`oidc-client-ts`) exchanged for a Wayd JWT, or local username/password

## 🚀 Quick Start

The [developer documentation](https://wayd.dev/docs/contributing/prerequisites) covers each step in full: [Prerequisites](docs/contributing/prerequisites.mdx), [Installation](docs/contributing/installation.mdx), [Configuration](docs/contributing/configuration.mdx), [First-Run Setup](docs/contributing/first-run-setup.mdx), and [Local Development](docs/contributing/local-development.mdx).

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 24+](https://nodejs.org/) and npm
- SQL Server 2019+ — the `mssql` service in `docker-compose.yml` (needs [Docker](https://www.docker.com/)), or LocalDB on Windows, or another instance
- [Git](https://git-scm.com/)

### 1. Clone and Restore

```bash
git clone https://github.com/destacey/Wayd.git
cd Wayd
git config core.hooksPath .githooks
dotnet tool restore
dotnet dev-certs https --trust
```

`dotnet tool restore` installs the pinned `dotnet ef`. On Linux, the dev certificate also needs `SSL_CERT_DIR` — see [Prerequisites](docs/contributing/prerequisites.mdx#https-development-certificate).

### 2. Configure the Database

The checked-in Development configuration points at the `mssql` service in `docker-compose.yml`:

```bash
cp .env.example .env      # optional - override the SA password or host port
docker compose up mssql
```

To use another server, set **both** connection strings in the API's user secrets (Hangfire reads its own). Aspire reads its connection string from `database.json` and user secrets, not from `database.Development.json`, so set these when running through Aspire as well:

```bash
cd Wayd.Web/src/Wayd.Web.Api
dotnet user-secrets set "DatabaseSettings:ConnectionString" "<your connection string>"
dotnet user-secrets set "HangfireSettings:Storage:ConnectionString" "<your connection string>"
```

The API applies migrations and seeds reference data on startup.

### 3. Run with .NET Aspire (Recommended)

```bash
cd Wayd.AppHost
dotnet run
```

- **Aspire Dashboard**: <https://localhost:17153> (telemetry, logs, traces, metrics; <http://localhost:15206> with the `http` profile)
- **Client (Next.js)**: <http://localhost:3000>
- **API**: <http://localhost:5150> / <https://localhost:7021>
- **API reference**: `{API URL}/scalar`

On Linux without a trusted certificate, run `export ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` and `dotnet run --launch-profile http`.

### 4. Create the First Administrator

On first boot with an empty database, the API prints a one-time **bootstrap token** to its log (`WAYD FIRST-RUN SETUP`). Open `/setup` in the client, paste the token, and create the first administrator account. See [First-Run Setup](docs/contributing/first-run-setup.mdx).

### 5. Configure Sign-In

Local (Wayd) username/password sign-in works out of the box. To let your team sign in with Microsoft Entra ID or another OIDC provider, add a provider in the app under **Settings → Access → Identity Providers** — identity providers are stored in the database, not in configuration files. See the [Entra app registration guide](docs/contributing/entra-app-registration.mdx) and [Configuration → Authentication](docs/contributing/configuration.mdx#authentication).

Every sign-in ends in a Wayd JWT signed with `SecuritySettings:LocalJwt:Secret`. The checked-in `security.json` holds development-only values for it and for `SecuritySettings:DataProtection:MasterKey`; a non-Development host refuses to start with the placeholder JWT secret.

### Alternative: Run with Docker Compose

```bash
cp .env.example .env
docker compose up
```

The client container gets its API address from `API_BASE_URL` in `.env`, and the API container mounts your user secrets and dev certificate from the paths `.env` describes — see [Installation](docs/contributing/installation.mdx#option-3-docker-compose).

- Client: <http://localhost:5002>
- API: <https://localhost:5001> (<http://localhost:5000>)
- API reference: <https://localhost:5001/scalar>
- Seq (logs): <http://localhost:8081>

Environment variable changes require a full teardown: `docker compose down && docker compose up`.

### Alternative: Run Components Individually

**Terminal 1 - API:**

```bash
cd Wayd.Web/src/Wayd.Web.Api
dotnet run
```

**Terminal 2 - Client:**

```bash
cd Wayd.Web/src/wayd.web.reactclient
npm install
npm run dev
```

Put the API's address in a gitignored `Wayd.Web/src/wayd.web.reactclient/.env.local` (`NEXT_PUBLIC_API_BASE_URL='http://localhost:5150'`). The client is at <http://localhost:3000>.

## 🧪 Testing

```bash
# All tests - the integration suites start SQL Server containers, so Docker must be running
dotnet test Wayd.slnx

# Only the tests that need no Docker
./.github/scripts/dotnet-test-projects.sh unit

# Architecture tests (enforce Clean Architecture rules)
dotnet test Wayd.ArchitectureTests/Wayd.ArchitectureTests.csproj

# A specific project
dotnet test "Wayd.Services/Wayd.Work/tests/Wayd.Work.Application.Tests/Wayd.Work.Application.Tests.csproj"

# Client
cd Wayd.Web/src/wayd.web.reactclient && npm run typecheck && npm run lint && npm test
```

See [Testing](docs/contributing/testing.mdx).

## 📊 Database Migrations

Run from the repository root, after `dotnet tool restore`.

**Add migration:**

```bash
dotnet ef migrations add <MigrationName> \
  --project "Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL" \
  --startup-project "Wayd.Web/src/Wayd.Web.Api"
```

**Apply migrations** (the API also applies them on startup):

```bash
dotnet ef database update \
  --project "Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL" \
  --startup-project "Wayd.Web/src/Wayd.Web.Api"
```

To reset a local database, use **Reset Database** on the `wayd-api` resource in the Aspire dashboard — see [Local Development](docs/contributing/local-development.mdx#resetting-the-database).

## 🏛️ Architecture

Wayd follows **Clean Architecture** with **Domain-Driven Design** principles:

```text
┌─────────────────────────────────────────┐
│  Presentation (Web API, Next.js Client) │
├─────────────────────────────────────────┤
│  Infrastructure (EF Core, Integrations) │
├─────────────────────────────────────────┤
│  Application (CQRS, Commands, Queries)  │
├─────────────────────────────────────────┤
│  Domain (Entities, Value Objects, Rules)│
└─────────────────────────────────────────┘
```

**Key Patterns:**

- CQRS with Wolverine
- Repository-less (direct DbContext usage)
- Vertical slice architecture per domain
- Functional error handling with Result pattern

See [Architecture](docs/contributing/architecture.mdx) and [AGENTS.md](AGENTS.md).

## 🔧 Troubleshooting

### Database Connection Issues

- Verify SQL Server is running (`docker compose up mssql` for the default configuration)
- Check both `DatabaseSettings:ConnectionString` and `HangfireSettings:Storage:ConnectionString` — see [Prerequisites → Database](docs/contributing/prerequisites.mdx#database)
- Ensure the database user has proper permissions

### Authentication Issues

- **No first-run token in the log:** a user already exists; sign in with that account. The setup page returns 404 once an administrator exists.
- **Locked out:** an administrator can unlock the account under **Settings → Access → Users**.
- **Login page renders with nothing on it:** usually CORS — reach the client and API on the addresses Aspire lists, not the ports the processes bind, and check `CorsSettings:WebClient`. See [Local Development](docs/contributing/local-development.mdx#use-the-proxy-ports-not-the-ones-the-processes-bind).
- **Entra ID sign-in fails:** see the troubleshooting section of the [Entra app registration guide](docs/contributing/entra-app-registration.mdx#troubleshooting).

### Aspire Dashboard Not Loading

- Check if port 17153 (or 15206 with the `http` profile) is available
- No Aspire workload is needed: the AppHost restores `Aspire.AppHost.Sdk` from NuGet, so `dotnet restore` in `Wayd.AppHost` is enough

### Next.js Build Errors

- Clear Next.js cache: `cd Wayd.Web/src/wayd.web.reactclient && rm -rf .next`
- Reinstall dependencies: `npm clean-install`

## 🤝 Contributing

See our [Contributing Guide](CONTRIBUTING.md) for detailed instructions on how to contribute to Wayd.

**Quick checklist:**

1. Fork the repository
2. Create a branch (`git checkout -b feat/123-amazing-feature`) — see [Branches and Commits](docs/contributing/git-workflow.mdx)
3. Commit your changes (`git commit -m 'feat(ppm): add amazing feature (#123)'`)
4. Push to the branch (`git push origin feat/123-amazing-feature`)
5. Open a Pull Request to the `main` branch

**Before submitting:** run the checks that cover your change, listed under "Before You Finish" in [AGENTS.md](AGENTS.md#before-you-finish).

## 📖 Additional Resources

- **Full Documentation**: <https://wayd.dev>
- **Architecture Guide**: [AGENTS.md](AGENTS.md)
- **API Documentation**: Available at `/scalar` when API is running

## Deployment

If you plan to use the client container, the following environment variable must be set (the container refuses to start without it):

```env
NEXT_PUBLIC_API_BASE_URL='{Your API URL}'
```

The client needs no identity-provider environment variables. The login page discovers configured identity providers at runtime from `GET /api/auth/providers`; providers are managed in the database via **Settings → Access → Identity Providers**.

The API container needs the following:

```env
CorsSettings__WebClient={your client URL}
DatabaseSettings__ConnectionString={connection string to your database}
HangfireSettings__Storage__ConnectionString={connection string to your database}
SecuritySettings__LocalJwt__Secret={strong random secret, at least 32 chars}
SecuritySettings__DataProtection__MasterKey={base64 32-byte key: openssl rand -base64 32}
```

`MasterKey` encrypts connector credentials (such as Azure DevOps PATs) at rest; losing it means re-entering them, so keep it separate from the JWT secret and back it up — see [Data Protection](docs/contributing/configuration.mdx#data-protection-at-rest-secret-encryption). Also override the Hangfire dashboard's checked-in credentials (`HangfireSettings__Credentials__User` / `HangfireSettings__Credentials__Password`), and set `ApiSettings__PublicUrl` and `ApiSettings__WebClientUrl` for MCP clients to sign in. The full list is in [Configuration → Environment Variables](docs/contributing/configuration.mdx#environment-variables).

> **Entra ID login** and the **Microsoft Graph people-sync connector** are both configured in the database, not via environment variables. Manage login providers under **Settings → Access → Identity Providers** and the Graph connection under **Settings → System → Connections**. See the [identity provider configuration docs](https://wayd.dev/docs/contributing/configuration).

Optional local JWT settings (with defaults):

```env
SecuritySettings__LocalJwt__TokenExpirationInMinutes=60
SecuritySettings__LocalJwt__RefreshTokenExpirationInDays=7
```

`Issuer` and `Audience` are JWT claim identifiers sourced from `security.json` — they're not environment-specific and shouldn't be overridden per deploy.

By default Wayd logs to the console via Serilog. The API also ships the Seq, Datadog, File, and OpenTelemetry Serilog sinks; configure one with `Serilog__Using__x` and `Serilog__WriteTo__x` env vars on the API container. Setting `OTEL_EXPORTER_OTLP_ENDPOINT` turns on the OpenTelemetry sink and exporter, and `APPLICATIONINSIGHTS_CONNECTION_STRING` sends OpenTelemetry data to Azure Application Insights. An example with Datadog (taken from some TF for the `azurerm_container_app` resource type):

```terraform
      env {
        name  = "Serilog__Using__1"
        value = "Serilog.Sinks.Datadog.Logs"
      }

      env {
        name  = "Serilog__WriteTo__1__Name"
        value = "DatadogLogs"
      }

      env {
        name  = "Serilog__WriteTo__1__Args__apiKey"
        value = var.dd_api_key
      }

      env {
        name  = "Serilog__WriteTo__1__Args__source"
        value = "csharp"
      }

      env {
        name  = "Serilog__WriteTo__1__Args__host"
        value = "wayd-api"
      }

      env {
        name  = "Serilog__WriteTo__1__Args__tags__0"
        value = "product:wayd"
      }

      env {
        name  = "Serilog__WriteTo__1__Args__tags__1"
        value = "service:wayd-api"
      }
```

> Note, if you use `__0` for the WriteTo and Using values, you will override the default console logging

## License

See [LICENSE](LICENSE.md)
