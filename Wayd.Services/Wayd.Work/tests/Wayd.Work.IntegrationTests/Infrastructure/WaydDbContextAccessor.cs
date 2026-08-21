using Wayd.Infrastructure.Persistence.Context;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Owns one <see cref="WaydDbContext"/> for the duration of a test and exposes it as the
/// <see cref="IWorkDbContext"/> a handler expects. A fresh context per act/assert step keeps the
/// change tracker from serving a cached entity and hiding what the database actually holds —
/// which matters here, because the handler writes through a set-based update that bypasses it.
/// </summary>
public sealed class WaydDbContextAccessor(SqlServerDbContextFixture fixture) : IAsyncDisposable
{
    private readonly WaydDbContext _context = fixture.CreateContext();

    public WaydDbContext Context => _context;

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();
}
