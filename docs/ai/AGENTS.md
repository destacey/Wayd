# AGENTS.md - Wayd Agent Context

This file provides context for AI coding agents working with the Wayd codebase.

## Quick Start

- **Build**: `dotnet build Wayd.slnx`
- **Test**: `dotnet test Wayd.slnx`
- **Frontend**: `cd Wayd.Web/src/wayd.web.reactclient && npm install && npm run dev`
- **Full stack**: `cd Wayd.AppHost && dotnet run`

## Architecture at a Glance

Wayd is a **modular monolith** using **Clean Architecture** with **DDD**. The dependency flow is:

```
Domain → Application → Infrastructure → Web API
```

Each domain service is a vertical slice:

```
Wayd.Services/Wayd.{Service}/
  src/Wayd.{Service}.Domain/       # Entities, value objects (zero dependencies)
  src/Wayd.{Service}.Application/  # Commands, queries, DTOs (depends on Domain)
  tests/                           # Mirror of src structure
```

All services share a single `WaydDbContext` (no repository pattern).

## Domain Services

| Service                    | Namespace                         | Purpose                                           |
| -------------------------- | --------------------------------- | ------------------------------------------------- |
| Work                       | `Wayd.Work`                       | Work items, workspaces, work processes, workflows |
| Organization               | `Wayd.Organization`               | Teams, employees, team memberships                |
| Planning                   | `Wayd.Planning`                   | Planning intervals, iterations                    |
| Goals                      | `Wayd.Goals`                      | Objectives, key results                           |
| ProjectPortfolioManagement | `Wayd.ProjectPortfolioManagement` | Projects, portfolios                              |
| StrategicManagement        | `Wayd.StrategicManagement`        | Strategic themes                                  |
| AppIntegration             | `Wayd.AppIntegration`             | External system connections                       |
| Links                      | `Wayd.Links`                      | Cross-entity associations                         |

## Patterns to Follow

### Adding a Command

```csharp
// 1. Command record (Application layer)
public sealed record CreateThingCommand(string Name) : ICommand<Guid>;

// 2. Validator (FluentValidation, auto-registered)
public sealed class CreateThingCommandValidator : AbstractValidator<CreateThingCommand>
{
    public CreateThingCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

// 3. Handler (returns Result<T>, uses DbContext directly). Handlers must be public —
//    Wolverine generates code that calls them and cannot invoke internal types.
public sealed class CreateThingHandler : ICommandHandler<CreateThingCommand, Guid>
{
    private readonly WaydDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public async Task<Result<Guid>> Handle(CreateThingCommand request, CancellationToken ct)
    {
        // Domain logic here
        var thing = Thing.Create(request.Name, _dateTimeProvider.Now);
        _context.Things.Add(thing);
        await _context.SaveChangesAsync(ct);
        return Result.Success(thing.Id);
    }
}
```

### Adding a Query

```csharp
public sealed record GetThingQuery(Guid Id) : IQuery<ThingDto>;

public sealed class GetThingHandler : IQueryHandler<GetThingQuery, ThingDto>
{
    private readonly WaydDbContext _context;

    public async Task<Result<ThingDto>> Handle(GetThingQuery request, CancellationToken ct)
    {
        var dto = await _context.Things
            .Where(t => t.Id == request.Id)
            .ProjectToType<ThingDto>()
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result.Failure<ThingDto>("Thing not found.")
            : Result.Success(dto);
    }
}
```

### Adding an API Endpoint

```csharp
[HttpPost]
public async Task<ActionResult<Guid>> Create(CreateThingRequest request, CancellationToken ct)
{
    var result = await _dispatcher.Send(request.ToCommand(), ct);
    return result.IsSuccess
        ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
        : BadRequest(result.Error);
}
```

### Adding a Frontend Page

```typescript
// Use NSwag-generated client, never authenticatedFetch()
import { getThingsClient } from "@/src/services/clients";

// Use Ant Design theme tokens, never hardcoded colors
// Use CSS modules with var(--ant-color-primary) etc.
```

## Rules for Agents

1. **Never use `DateTime.UtcNow`** - inject `IDateTimeProvider`
2. **Never hardcode colors** - use Ant Design theme tokens
3. **Never use `authenticatedFetch()`** - use NSwag-generated typed clients
4. **No `Async` suffix** on new async methods
5. **No repository pattern** - use `WaydDbContext` directly
6. **Return `Result<T>`** from handlers, not exceptions
7. **Use NodaTime** (`Instant`, `LocalDate`) instead of `DateTime`
8. **Central Package Management** - versions in `Directory.Packages.props` only
9. **Architecture tests exist** - run `dotnet test Wayd.ArchitectureTests` to verify dependency rules
10. **Feature flags** - new features behind flags defined in `FeatureFlags.cs`

## Key File Locations

| What                | Where                                                                              |
| ------------------- | ---------------------------------------------------------------------------------- |
| Solution file       | `Wayd.slnx`                                                                        |
| Package versions    | `Directory.Packages.props`                                                         |
| DB Context          | `Wayd.Infrastructure/src/Wayd.Infrastructure/Persistence/Context/WaydDbContext.cs` |
| Entity configs      | `Wayd.Infrastructure/src/Wayd.Infrastructure/Persistence/Configuration/`           |
| Migrations          | `Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL/`                     |
| Feature flags       | `Wayd.Common/src/Wayd.Common.Domain/FeatureManagement/FeatureFlags.cs`             |
| API controllers     | `Wayd.Web/src/Wayd.Web.Api/Controllers/`                                           |
| Generated TS client | `Wayd.Web/src/wayd.web.reactclient/src/services/wayd-api.ts`                       |
| Client factories    | `Wayd.Web/src/wayd.web.reactclient/src/services/clients.ts`                        |
| Frontend pages      | `Wayd.Web/src/wayd.web.reactclient/src/app/`                                       |
| Shared test fakers  | `Wayd.Common/tests/Wayd.Tests.Shared/`                                             |
| Architecture tests  | `Wayd.ArchitectureTests/`                                                         ests.Shared/` |
| Architecture tests | `Wayd.ArchitectureTests/` |
