using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Persistence;

public interface IWaydDbContext
{
    // this dependency is bigger than needed, but most of the extensions methods are leveraging it.
    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry Entry(object entity);

    // Common DbSets
    DbSet<Employee> Employees { get; }
    DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems { get; }
    DbSet<OidcProvider> OidcProviders { get; }
    DbSet<PersonalAccessToken> PersonalAccessTokens { get; }
    DbSet<User> WaydUsers { get; }
    DbSet<ScoringModel> ScoringModels { get; }
}
