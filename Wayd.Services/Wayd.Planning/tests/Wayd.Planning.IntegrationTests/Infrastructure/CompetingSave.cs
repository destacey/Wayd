using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wayd.Planning.IntegrationTests.Infrastructure;

/// <summary>
/// Commits another request's change, from a second context, just before the first save of the context under
/// test reaches the database: the request that wins a race between the handler's load and its save.
/// </summary>
internal sealed class CompetingSave(Func<CancellationToken, Task> competitor) : SaveChangesInterceptor
{
    /// <summary>Whether the competing change was committed.</summary>
    public bool Made { get; private set; }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!Made)
        {
            Made = true;
            await competitor(cancellationToken);
        }

        return result;
    }
}
