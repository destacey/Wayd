using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Records every entity a context writes, as EF sees it at the moment of saving.
/// </summary>
/// <remarks>
/// For proving a handler leaves an entity alone. Reading the row back afterwards cannot show that: an
/// update that rewrote the same values leaves it looking untouched, and the change-tracker state that
/// decides whether an entity is inserted, updated or ignored is exactly what the in-memory fakes do not
/// model.
/// </remarks>
public sealed class SavedEntityRecorder : SaveChangesInterceptor
{
    private readonly List<(Type Type, EntityState State)> _writes = [];

    public IReadOnlyList<(Type Type, EntityState State)> Writes => _writes;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _writes.AddRange(eventData.Context!.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (e.Entity.GetType(), e.State)));

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
