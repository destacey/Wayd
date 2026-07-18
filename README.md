# Wayd

**An intelligent delivery management platform designed to give engineering leaders and teams end-to-end visibility into software delivery.**

When delivery spans multiple teams, projects, and systems, visibility breaks down. Wayd brings it all together — tracking work items, aligning teams to planning intervals and products, and surfacing dependencies across the organization.

**Core Philosophy:** Wayd acts as a unified hub that synchronizes data from multiple business systems and combines it with capabilities those systems lack — connecting the dots so teams can see the full picture in one place.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)

## 📚 Documentation

[Full Documentation](https://wayd.dev) | [Contributing Guide](CONTRIBUTING.md)

## ✨ Key Features

- Work Item Management - Track tasks, bugs, user stories, and dependencies across multiple systems
- Team Organization - Manage teams, team-of-teams, and organizational structure
- Planning - Planning intervals, sprint planning, and roadmaps
- Dependency Tracking - Visualize and manage cross-team and cross-project dependencies
- Portfolio Management - Project and portfolio tracking with organizational alignment
- External Integrations - Sync teams, work items, iterations, and sprint data from external systems like Azure DevOps on automated cycles
- User Management - Local user accounts with JWT auth, role-based access control, password management, and account lifecycle (activate/deactivate/unlock)
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
- Azure AD (MSAL) or local JWT authentication

## 🚀 Quick Start

### Prerequisites

Before you begin, ensure you have installed:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/) and npm
- [SQL Server](https://www.microsoft.com/sql-server) (local or remote)
- [Git](https://git-scm.com/)

### 1. Clone the Repository

```bash
git clone https://github.com/destacey/Wayd.git
cd Wayd
```

### 2. Configure Database

Create or update `Wayd.Web/src/Wayd.Web.Api/Configurations/database.json`:

```json
{
  "DatabaseSettings": {
    "DBProvider": "mssql",
    "ConnectionString": "Server=localhost;Database=Wayd;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 3. Configure Authentication

Wayd supports two authentication methods: **Microsoft Entra ID (Azure AD)** for directory-based SSO, and **Wayd (Local)** for self-contained username/password authentication with JWT tokens. You can use either or both — each user is assigned a login provider at creation time.

#### Option A: Local Authentication (Simplest)

No external identity provider needed. Configure a JWT signing secret in `Wayd.Web/src/Wayd.Web.Api/Configurations/security.json` (or user secrets):

```json
{
  "SecuritySettings": {
    "LocalJwt": {
      "Secret": "<generate-a-strong-random-secret-at-least-32-chars>"
    }
  }
}
```

On first run, the database seeder creates a default admin user (`admin@wayd.local` / `Password1!`). You will be prompted to change the password on first login.

Create a `.env` file in the repository root:

```env
API_BASE_URL='http://localhost:5000'
```

#### Option B: Microsoft Entra ID (Azure AD)

You'll need an Azure AD app registration. The same `.env` as Option A is all the environment needs:

```env
API_BASE_URL='http://localhost:5000'
```

Entra ID itself is configured **in the app**, not via environment variables: sign in as an admin and add a provider under **Settings → Identity Providers** (authority, client ID, scopes, audience, allowed tenants). See the [Entra app registration guide](docs/contributing/entra-app-registration.mdx) for collecting those values and [configuration docs](docs/contributing/configuration.mdx) for the identity-provider model.

### 4. Run with .NET Aspire (Recommended)

The easiest way to run Wayd locally with full observability:

```bash
cd Wayd.AppHost
dotnet run
```

> **Note**: if you haven't trusted the developer certificate yet, on Windows/macOS you can run `dotnet dev-certs https --trust` to trust it. Otherwise, on Linux you should call
>
> ```bash
> export ASPIRE_ALLOW_UNSECURED_TRANSPORT=true
> dotnet run --launch-profile http
> ```
>
> to run without HTTPS.

**Access the application:**

- **Aspire Dashboard**: <http://localhost:15888> (telemetry, logs, traces, metrics)
- **Client (Next.js)**: <http://localhost:3000>
- **API**: Check Aspire dashboard for dynamically assigned port
- **API Swagger**: `{API URL}/swagger`

The database will be automatically created and migrations applied on first run.

### Alternative: Run with Docker Compose

```bash
docker compose up
```

**Access points:**

- Client: <http://localhost:5002>
- API: <https://localhost:5001>
- API Swagger: <https://localhost:5001/swagger>
- Seq (logs): <http://localhost:8081>

**Note:** Environment variable changes in Docker require full teardown: `docker compose down && docker compose up`

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

Access at <http://localhost:3000>

## 🧪 Testing

**Run all tests:**

```bash
dotnet test Wayd.slnx
```

**Run architecture tests (enforce Clean Architecture rules):**

```bash
dotnet test Wayd.ArchitectureTests/Wayd.ArchitectureTests.csproj
```

**Run specific test project:**

```bash
dotnet test "Wayd.Services/Wayd.Work/tests/Wayd.Work.Application.Tests/Wayd.Work.Application.Tests.csproj"
```

## 📊 Database Migrations

**Add migration:**

```bash
dotnet ef migrations add <MigrationName> \
  --project "Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL" \
  --startup-project "Wayd.Web/src/Wayd.Web.Api"
```

**Apply migrations:**

```bash
dotnet ef database update \
  --project "Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL" \
  --startup-project "Wayd.Web/src/Wayd.Web.Api"
```

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

See [CLAUDE.md](CLAUDE.md) for detailed architecture documentation.

## 🔧 Troubleshooting

### Database Connection Issues

- Verify SQL Server is running
- Check connection string in `database.json`
- Ensure database user has proper permissions

### Authentication Issues

**Local Auth:**

- Ensure `SecuritySettings:LocalJwt:Secret` is configured (at least 32 characters)
- Default admin credentials: `admin@wayd.local` / `Password1!` (must change on first login)
- If locked out, an admin can unlock accounts via Settings > User Management

**Azure AD:**

- Verify Azure AD app registration configuration
- Check `.env` file values match your Azure AD setup
- Ensure redirect URIs are configured in Azure AD

### Aspire Dashboard Not Loading

- Check if port 15888 is available
- Verify .NET Aspire workload is installed: `dotnet workload list`
- Install if needed: `dotnet workload install aspire`

### Next.js Build Errors

- Clear Next.js cache: `cd Wayd.Web/src/wayd.web.reactclient && rm -rf .next`
- Reinstall dependencies: `npm clean-install`

## 🤝 Contributing

See our [Contributing Guide](CONTRIBUTING.md) for detailed instructions on how to contribute to Wayd.

**Quick checklist:**

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request to the `main` branch

**Before submitting:**

- Run tests: `dotnet test Wayd.slnx`
- Run architecture tests: `dotnet test Wayd.ArchitectureTests/Wayd.ArchitectureTests.csproj`
- Ensure code follows existing patterns (see [CLAUDE.md](CLAUDE.md))

## 📖 Additional Resources

- **Full Documentation**: <https://wayd.dev>
- **Architecture Guide**: [CLAUDE.md](CLAUDE.md)
- **API Documentation**: Available at `/swagger` when API is running

## Deployment

If you plan to use the client container, the following environment variable must be set:

```env
NEXT_PUBLIC_API_BASE_URL='{Your API URL}'
```

The client no longer needs any Entra/OIDC environment variables. The login page discovers configured identity providers at runtime from `GET /api/auth/providers`; providers are managed in the database via **Settings → Identity Providers**.

The API container needs the following:

```env
CorsSettings__WebClient={your client URL}
DatabaseSettings__ConnectionString={connection string to your database}
HangfireSettings__Storage__ConnectionString={connection string to your database}
```

> **Entra ID login** and the **Microsoft Graph people-sync connector** are both configured in the database, not via environment variables. Manage login providers under **Settings → Identity Providers** and the Graph connection under **Settings → Connections**. See the [identity provider configuration docs](https://wayd.dev/docs/contributing/configuration).

**For local (Wayd) authentication:**

```env
SecuritySettings__LocalJwt__Secret={strong random secret, at least 32 chars}
```

Optional local JWT settings (with defaults):

```env
SecuritySettings__LocalJwt__TokenExpirationInMinutes=60
SecuritySettings__LocalJwt__RefreshTokenExpirationInDays=7
```

`Issuer` and `Audience` are JWT claim identifiers sourced from `security.json` — they're not environment-specific and shouldn't be overridden per deploy.

Additionally, by default Wayd logs to the console via Serilog. If you wish to configure any of the other supported sinks (currently Seq, Datadog and Application Insights), provide the appropriate Serilog**Using**x and Serilog**WriteTo**x settings as env vars for your wayd-api container. An example with DataDog (taken from some TF for the `azurerm_container_app` resource type):

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
