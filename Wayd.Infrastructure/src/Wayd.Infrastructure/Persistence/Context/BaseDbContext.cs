using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.Common.Services;
using Wayd.Infrastructure.Messaging;
using Wayd.Infrastructure.Persistence.Extensions;
using NodaTime;
using Wolverine.EntityFrameworkCore;

namespace Wayd.Infrastructure.Persistence.Context;

public abstract class BaseDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string, IdentityUserClaim<string>, ApplicationUserRole, IdentityUserLogin<string>, ApplicationRoleClaim, IdentityUserToken<string>>
{
    protected readonly ICurrentUser _currentUser;
    protected readonly IDateTimeProvider _dateTimeProvider;
    private readonly DatabaseSettings _dbSettings;
    private readonly IEventPublisher _events;
    private readonly IDbContextOutbox _outbox;
    private readonly IRequestCorrelationIdProvider _requestCorrelationIdProvider;

    protected BaseDbContext(DbContextOptions options, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, IOptions<DatabaseSettings> dbSettings, IEventPublisher events, IDbContextOutbox outbox, IRequestCorrelationIdProvider requestCorrelationIdProvider)
        : base(options)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _dbSettings = dbSettings.Value;
        _events = events;
        _outbox = outbox;
        _requestCorrelationIdProvider = requestCorrelationIdProvider;

        // this is need so that the owned entities are soft deleted correctly
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;
    }

    // Used by Dapper
    public IDbConnection Connection => Database.GetDbConnection();

    public DbSet<Trail> AuditTrails => Set<Trail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.Entity<Kpi>().UseTpcMappingStrategy();
        //modelBuilder.Entity<KpiCheckpoint>().UseTpcMappingStrategy();
        //modelBuilder.Entity<KpiMeasurement>().UseTpcMappingStrategy();

        // QueryFilters need to be applied before base.OnModelCreating
        modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(s => !s.IsDeleted);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // add shadow properties for entities that implement ISystemAuditable
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISystemAuditable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Property<Instant>("SystemCreated");
                modelBuilder.Entity(entityType.ClrType).Property<string?>("SystemCreatedBy").HasMaxLength(450);
                modelBuilder.Entity(entityType.ClrType).Property<Instant>("SystemLastModified");
                modelBuilder.Entity(entityType.ClrType).Property<string?>("SystemLastModifiedBy").HasMaxLength(450);
            }
        }

        // configure max length for user ID columns on soft-deletable entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Property(nameof(ISoftDelete.DeletedBy)).HasMaxLength(450);
            }
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // TODO: We want this only for development probably... maybe better make it configurable in logger.json config?
        optionsBuilder.EnableSensitiveDataLogging();

        // If you want to see the sql queries that efcore executes:

        // Uncomment the next line to see them in the output window of visual studio
        // optionsBuilder.LogTo(m => Debug.WriteLine(m), LogLevel.Information);

        // Or uncomment the next line if you want to see them in the console
        // optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);

        optionsBuilder.UseDatabase(_dbSettings.DBProvider!, _dbSettings.ConnectionString!);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        var auditEntries = HandleAuditingBeforeSaveChanges(_currentUser.GetUserId(), _requestCorrelationIdProvider.CorrelationId);

        int result = await base.SaveChangesAsync(cancellationToken);

        await HandleAuditingAfterSaveChangesAsync(auditEntries, cancellationToken);

        // Raise the deferred post-persistence domain events AFTER the entity has been saved. Several aggregates
        // carry a database-generated sequential Key (ValueGeneratedOnAdd) that the events capture (e.g.
        // TeamCreatedEvent), so the events must be built once EF has assigned it — running them before the save
        // would capture a default (0) Key. Actions and events are cleared as they are drained, so if
        // HandleAuditingAfterSaveChangesAsync above re-entered SaveChanges (temp-property audit trails), the
        // events are raised there and this outer pass is a no-op — each is raised exactly once, post-save.
        ExecutePostPersistenceActions();

        // Route the raised events (see DurableEventRoutes): durable events are enlisted in the Wolverine EF
        // Core outbox (their OutgoingMessage envelope rows are staged into this change tracker, then committed
        // by the second save below); inline events are dispatched synchronously after the commit, preserving
        // read-your-writes for the cross-domain replication projections. Events are drained from the aggregates
        // as they are routed.
        var (inlineEvents, enrolledDurableEvents) = await EnlistDomainEvents();

        // Commit the staged durable envelopes. This is a second, small transaction — the outbox envelope is
        // therefore not committed atomically with the entity change, but it IS durably persisted before it is
        // flushed to the sending agents, so a crash after this point still delivers via the recovery agent.
        // (Post-persistence events capture a DB-generated Key, so they cannot be enlisted before the entity
        // save; that rules out a single atomic write here.)
        if (enrolledDurableEvents)
        {
            await base.SaveChangesAsync(cancellationToken);

            // Hand the committed envelopes to the sending agents for background delivery. Flush before the
            // inline dispatch so a durable event is on its way even if an inline handler throws; the durable
            // path has its own retry/dead-letter policy.
            await _outbox.FlushOutgoingMessagesAsync();
        }

        foreach (var inlineEvent in inlineEvents)
        {
            await _events.PublishAsync(inlineEvent);
        }

        return result;
    }

    private List<AuditTrail> HandleAuditingBeforeSaveChanges(string userId, string correlationId)
    {
        var timestamp = _dateTimeProvider.Now;
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.Entity is ISystemAuditable or ISoftDelete)
            .ToList())
        {

            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is ISystemAuditable)
                {
                    entry.Property("SystemCreated").CurrentValue = timestamp;
                    entry.Property("SystemCreatedBy").CurrentValue = userId;
                }
            }

            if (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.HasChangedOwnedEntities())
            {
                if (entry.Entity is ISystemAuditable)
                {
                    entry.Property("SystemLastModified").CurrentValue = timestamp;
                    entry.Property("SystemLastModifiedBy").CurrentValue = userId;
                }
            }

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDelete)
            {
                softDelete.IsDeleted = true;
                softDelete.DeletedBy = userId;
                softDelete.Deleted = timestamp;
                entry.State = EntityState.Modified;
            }
        }

        ChangeTracker.DetectChanges();

        var trailEntries = new List<AuditTrail>();

        var auditableEntries = ChangeTracker.Entries()
            .Where(e =>
                e.Entity is ISystemAuditable
                && (e.State is EntityState.Added or EntityState.Deleted or EntityState.Modified
                    || e.HasChangedOwnedEntities()))
            .ToList();

        foreach (var entry in auditableEntries)
        {
            // TODO: Fix the table name.  Currently, it is returning the class name, not the table name which may be different due to mappings.
            var trailEntry = new AuditTrail(entry, _dateTimeProvider)
            {
                SchemaName = entry.Metadata.GetSchema(),
                TableName = entry.Entity.GetType().Name,
                UserId = userId,
                CorrelationId = correlationId
            };
            trailEntries.Add(trailEntry);
            // Track which complex properties we've already processed
            var processedComplexProperties = new HashSet<string>();

            foreach (var property in entry.Properties)
            {
                if (property.IsTemporary)
                {
                    trailEntry.TemporaryProperties.Add(property);
                    continue;
                }

                string propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    trailEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                // Skip properties that belong to complex types - we'll handle them separately
                if (property.Metadata.DeclaringType is Microsoft.EntityFrameworkCore.Metadata.IComplexType)
                {
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        trailEntry.TrailType = TrailType.Create;
                        trailEntry.NewValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        trailEntry.TrailType = TrailType.Delete;
                        trailEntry.OldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        // TODO: IsModified appears to always be true
                        if (property.IsModified && ((property.OriginalValue is null && property.CurrentValue is not null) || property.OriginalValue?.Equals(property.CurrentValue) == false))
                        {
                            trailEntry.ChangedColumns.Add(propertyName);
                            trailEntry.TrailType = TrailType.Update;
                            trailEntry.OldValues[propertyName] = property.OriginalValue;
                            trailEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }

            // Include complex properties in the audit trail
            foreach (var complexProperty in entry.ComplexProperties)
            {
                string complexPropertyName = complexProperty.Metadata.Name;
                if (processedComplexProperties.Contains(complexPropertyName))
                    continue;

                processedComplexProperties.Add(complexPropertyName);

                switch (entry.State)
                {
                    case EntityState.Added:
                        trailEntry.NewValues[complexPropertyName] = GetComplexPropertyValues(complexProperty, isCurrentValue: true);
                        break;

                    case EntityState.Deleted:
                        trailEntry.OldValues[complexPropertyName] = GetComplexPropertyValues(complexProperty, isCurrentValue: false);
                        break;

                    case EntityState.Modified:
                        var hasChanges = complexProperty.Properties.Any(p =>
                            p.IsModified &&
                            ((p.OriginalValue is null && p.CurrentValue is not null) ||
                             p.OriginalValue?.Equals(p.CurrentValue) == false));

                        if (hasChanges)
                        {
                            trailEntry.ChangedColumns.Add(complexPropertyName);
                            trailEntry.OldValues[complexPropertyName] = GetComplexPropertyValues(complexProperty, isCurrentValue: false);
                            trailEntry.NewValues[complexPropertyName] = GetComplexPropertyValues(complexProperty, isCurrentValue: true);
                        }
                        break;
                }
            }

            // Include owned entities in the audit trail
            foreach (var reference in entry.References.Where(r => r.TargetEntry != null && r.TargetEntry.Metadata.IsOwned()))
            {
                var ownedEntry = reference.TargetEntry!;
                string navigationName = reference.Metadata.Name;

                switch (entry.State)
                {
                    case EntityState.Added:
                        if (ownedEntry.State == EntityState.Added)
                        {
                            trailEntry.NewValues[navigationName] = GetOwnedEntityValues(ownedEntry, isCurrentValue: true);
                        }
                        break;

                    case EntityState.Deleted:
                        if (ownedEntry.State == EntityState.Deleted)
                        {
                            trailEntry.OldValues[navigationName] = GetOwnedEntityValues(ownedEntry, isCurrentValue: false);
                        }
                        break;

                    case EntityState.Modified:
                        if (ownedEntry.State == EntityState.Added)
                        {
                            trailEntry.ChangedColumns.Add(navigationName);
                            trailEntry.NewValues[navigationName] = GetOwnedEntityValues(ownedEntry, isCurrentValue: true);
                        }
                        else if (ownedEntry.State == EntityState.Modified)
                        {
                            var hasChanges = ownedEntry.Properties.Any(p =>
                                p.IsModified &&
                                ((p.OriginalValue is null && p.CurrentValue is not null) ||
                                 p.OriginalValue?.Equals(p.CurrentValue) == false));

                            if (hasChanges)
                            {
                                trailEntry.ChangedColumns.Add(navigationName);
                                trailEntry.OldValues[navigationName] = GetOwnedEntityValues(ownedEntry, isCurrentValue: false);
                                trailEntry.NewValues[navigationName] = GetOwnedEntityValues(ownedEntry, isCurrentValue: true);
                            }
                        }
                        else if (ownedEntry.State == EntityState.Deleted)
                        {
                            trailEntry.ChangedColumns.Add(navigationName);
                            trailEntry.OldValues[navigationName] = GetOwnedEntityValues(ownedEntry, isCurrentValue: false);
                        }
                        break;
                }
            }

            // set trailtype to SoftDelete if the entity is soft deleted
            if (entry.State == EntityState.Modified && entry.Entity is ISoftDelete softDeleteEntity && softDeleteEntity.IsDeleted
                    && trailEntry.OldValues.TryGetValue("IsDeleted", out var oldIsDeletedValue) && oldIsDeletedValue is not null && (bool)oldIsDeletedValue == false)
            {
                trailEntry.TrailType = TrailType.SoftDelete;
            }
        }

        foreach (var auditEntry in trailEntries.Where(e => !e.HasTemporaryProperties))
        {
            AuditTrails.Add(auditEntry.ToAuditTrail());
        }

        return trailEntries.Where(e => e.HasTemporaryProperties).ToList();
    }

    private static Dictionary<string, object?> GetOwnedEntityValues(EntityEntry ownedEntry, bool isCurrentValue)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in ownedEntry.Properties)
        {
            // Skip primary keys and foreign keys for owned entities
            if (!property.Metadata.IsPrimaryKey() && !property.Metadata.IsForeignKey())
            {
                values[property.Metadata.Name] = isCurrentValue ? property.CurrentValue : property.OriginalValue;
            }
        }
        return values;
    }

    private static Dictionary<string, object?> GetComplexPropertyValues(ComplexPropertyEntry complexProperty, bool isCurrentValue)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in complexProperty.Properties)
        {
            values[property.Metadata.Name] = isCurrentValue ? property.CurrentValue : property.OriginalValue;
        }
        return values;
    }

    private Task HandleAuditingAfterSaveChangesAsync(List<AuditTrail> trailEntries, CancellationToken cancellationToken = new())
    {
        if (trailEntries == null || trailEntries.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var entry in trailEntries)
        {
            foreach (var prop in entry.TemporaryProperties)
            {
                if (prop.Metadata.IsPrimaryKey())
                {
                    entry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                }
                else
                {
                    entry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                }
            }

            AuditTrails.Add(entry.ToAuditTrail());
        }

        return SaveChangesAsync(cancellationToken);
    }

    private void ExecutePostPersistenceActions()
    {
        var entitiesWithActions = ChangeTracker.Entries<IEntity>()
            .Select(e => e.Entity)
            .Where(e => e.PostPersistenceActions.Count > 0)
            .ToArray();

        foreach (var entity in entitiesWithActions)
        {
            entity.ExecutePostPersistenceActions();
        }
    }

    /// <summary>
    /// Drains the domain events raised on tracked aggregates and routes each by its classification
    /// (see <see cref="DurableEventRoutes"/>):
    /// <list type="bullet">
    /// <item>
    /// <b>Durable</b> events are published through the enrolled Wolverine EF Core outbox, which stages
    /// their envelope rows into <em>this</em> change tracker. The caller commits them with a second
    /// <c>base.SaveChangesAsync</c> and then flushes the outbox for post-commit background delivery.
    /// </item>
    /// <item>
    /// <b>Inline</b> events are returned to the caller to be dispatched synchronously AFTER the commit via
    /// <see cref="IEventPublisher.PublishAsync"/>, preserving the read-your-writes contract the
    /// cross-domain replication projections rely on.
    /// </item>
    /// </list>
    /// Events are cleared from the aggregates as they are drained, so a re-entrant save (e.g. the audit
    /// temp-property pass) does not re-raise them.
    /// </summary>
    /// <returns>
    /// The inline events (in raise order) for the caller to dispatch after the commit, and whether any
    /// durable event was enrolled in the outbox this pass (so the caller knows whether to flush).
    /// </returns>
    private async Task<(List<IEvent> InlineEvents, bool EnrolledDurableEvents)> EnlistDomainEvents()
    {
        var entitiesWithEvents = ChangeTracker.Entries<IEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToArray();

        var inlineEvents = new List<IEvent>();
        var enrolled = false;

        foreach (var entity in entitiesWithEvents)
        {
            var domainEvents = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();
            foreach (var domainEvent in domainEvents)
            {
                if (DurableEventRoutes.IsDurable(domainEvent))
                {
                    // Enroll THIS DbContext instance (not the outbox's own scoped context) exactly once, so the
                    // envelope rows land in this change tracker and are committed by the caller's second save.
                    if (!enrolled)
                    {
                        _outbox.Enroll(this);
                        enrolled = true;
                    }

                    // PublishAsync on an enrolled outbox routes the message and persists its OutgoingMessage
                    // envelope into the change tracker (it does NOT save); base.SaveChangesAsync commits it.
                    // IMessageBus.PublishAsync takes DeliveryOptions, not a token — the envelope insert is
                    // part of the caller's SaveChangesAsync(cancellationToken) transaction.
                    await _outbox.PublishAsync(domainEvent);
                }
                else
                {
                    inlineEvents.Add(domainEvent);
                }
            }
        }

        return (inlineEvents, enrolled);
    }
}