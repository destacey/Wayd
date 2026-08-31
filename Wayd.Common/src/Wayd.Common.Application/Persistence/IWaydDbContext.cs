using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.StatusWorkflows;

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
    DbSet<ExternalIdentityMapping> ExternalIdentityMappings { get; }
    DbSet<OidcProvider> OidcProviders { get; }
    DbSet<PersonalAccessToken> PersonalAccessTokens { get; }
    DbSet<User> WaydUsers { get; }
    DbSet<ScoringModel> ScoringModels { get; }
}

/// <summary>
/// The workflow engine's own tables.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWaydDbContext"/> because the modules need it, not because Common does.
/// Any module whose records carry a status resolves one through <c>StatusResolver</c>, so its test
/// fake has to supply these tables — and if they hung off the global interface, that fake would owe an
/// implementation of the whole thing: <c>Database</c>, <c>ChangeTracker</c>, <c>Entry</c>, employees,
/// users, providers, scoring models, none of which the module touches.
/// <para>
/// <c>FakeProductManagementDbContext</c> implements this alongside its own context for exactly that
/// reason, and every module adopting the engine will do the same.
/// </para>
/// </remarks>
public interface IStatusWorkflowDbContext
{
    DbSet<StatusWorkflow> StatusWorkflows { get; }
    DbSet<WorkflowAssignment> WorkflowAssignments { get; }

    /// <summary>
    /// The status history of every tracked record, across every owner type.
    /// </summary>
    /// <remarks>
    /// Reached as a set rather than through the aggregate: the history is deliberately not a navigation,
    /// so <c>StatusTrackedEntity.StatusTransitions</c> holds only what the current instance has appended
    /// and is empty on a record loaded from the database. Reads are keyed by
    /// (<c>OwnerType</c>, <c>RecordId</c>) together — <c>RecordId</c> alone is only unique within an
    /// owner type.
    /// </remarks>
    DbSet<StatusTransition> StatusTransitions { get; }

    /// <summary>
    /// The per-owner-type display names for well-known aliases.
    /// </summary>
    /// <remarks>
    /// Read only for presentation. The authoritative vocabulary is the registered
    /// <c>WorkflowOwnerDescriptor</c>, which is always current; these rows exist so the same names are
    /// legible in SQL.
    /// </remarks>
    DbSet<WorkflowAliasName> WorkflowAliasNames { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
