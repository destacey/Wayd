using Microsoft.AspNetCore.Identity;
using Wayd.Common.Application.Identity;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events.Identity;
using NodaTime;

namespace Wayd.Infrastructure.Identity;

/// <remarks>
/// Derives from ASP.NET Identity's <see cref="IdentityUser"/> rather than <see cref="BaseEntity"/>, so it implements
/// <see cref="IEntity"/> itself for <c>SaveChanges</c> to drain its events. <see cref="UserManager{TUser}"/> saves
/// through the same context, so an event raised before a manager call is drained by that call's save. A manager
/// call can also fail without saving; the caller then calls <see cref="ClearDomainEvents"/>, or the event would be
/// recorded by the next save in the scope as though the change had happened.
/// </remarks>
public class ApplicationUser : IdentityUser, IEntity
{
    private readonly List<DomainEvent> _domainEvents = [];
    private readonly List<Action> _postPersistenceActions = [];

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Instant? LastActivityAt { get; set; }

    public string LoginProvider { get; set; } = null!;

    /// <summary>
    /// Staged tenant migration target. When set, an Entra login from this tenant
    /// triggers a transactional rebind of the user's active <see cref="UserIdentity"/>
    /// row in the exchange handler. Cleared on completion or admin cancellation.
    /// </summary>
    public string? PendingMigrationTenantId { get; set; }

    /// <summary>
    /// When the pending tenant migration was staged. Surfaced on the provider's
    /// "Active migrations" view. Set whenever <see cref="PendingMigrationTenantId"/>
    /// is set and cleared whenever it is cleared, so the two always move together.
    /// </summary>
    public Instant? PendingMigrationStagedAt { get; set; }

    /// <summary>
    /// Staged cross-provider migration target. When set, the user's next login via
    /// the named provider triggers a transactional rebind: the active
    /// <see cref="UserIdentity"/> row is deactivated and a new row for the target
    /// provider is inserted, preserving the user's <c>Id</c> and all downstream FKs.
    /// Cleared on completion or admin cancellation.
    /// </summary>
    public string? PendingMigrationProviderId { get; set; }

    public bool MustChangePassword { get; set; }

    /// <summary>
    /// One row per concurrent sign-in session. Sessions live here rather than in columns on
    /// this row so a second device does not displace the first, and so detected token reuse
    /// revokes only the affected session.
    /// </summary>
    public ICollection<UserRefreshToken> RefreshTokens { get; set; } = [];

    public UserPreferences Preferences { get; set; } = new();

    public ICollection<ApplicationUserRole> UserRoles { get; set; } = [];

    public ICollection<UserIdentity> Identities { get; set; } = [];

    public void RecordCreation(EventActor actor, Instant timestamp) =>
        AddDomainEvent(new ApplicationUserCreatedEvent(Id, actor, timestamp));

    public void UpdateDetails(string? firstName, string? lastName, string? email, string? phoneNumber, EventActor actor, Instant timestamp)
    {
        var before = (FirstName, LastName, Email, PhoneNumber);

        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;

        if (before != (FirstName, LastName, Email, PhoneNumber))
        {
            AddDomainEvent(new ApplicationUserDetailsUpdatedEvent(Id, actor, timestamp));
        }
    }

    public void ChangeEmployeeLink(Guid? employeeId, EventActor actor, Instant timestamp)
    {
        var previous = EmployeeId;
        if (previous == employeeId)
            return;

        EmployeeId = employeeId;
        AddDomainEvent(new ApplicationUserEmployeeLinkChangedEvent(Id, previous, employeeId, actor, timestamp));
    }

    /// <summary>
    /// Records a change to the user's role assignments, which <see cref="UserManager{TUser}"/> writes to the join
    /// table rather than to this entity. Raises nothing when the two sets hold the same roles.
    /// </summary>
    public void RecordRolesChange(IEnumerable<string> roleIdsBefore, IEnumerable<string> roleIdsAfter, EventActor actor, Instant timestamp)
    {
        var before = roleIdsBefore.ToHashSet();
        var after = roleIdsAfter.ToHashSet();

        string[] added = [.. after.Except(before).Order()];
        string[] removed = [.. before.Except(after).Order()];
        if (added.Length == 0 && removed.Length == 0)
            return;

        AddDomainEvent(new ApplicationUserRolesChangedEvent(Id, added, removed, [.. after.Order()], actor, timestamp));
    }

    public void Activate(EventActor actor, Instant timestamp)
    {
        if (IsActive)
            return;

        IsActive = true;
        AddDomainEvent(new ApplicationUserActivatedEvent(Id, actor, timestamp));
    }

    public void Deactivate(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
            return;

        IsActive = false;
        AddDomainEvent(new ApplicationUserDeactivatedEvent(Id, actor, timestamp));
    }

    public void StageTenantMigration(string targetTenantId, EventActor actor, Instant timestamp)
    {
        var previous = PendingMigrationTenantId;

        PendingMigrationTenantId = targetTenantId;
        PendingMigrationStagedAt = timestamp;

        AddDomainEvent(new ApplicationUserTenantMigrationStagedEvent(Id, targetTenantId, previous, actor, timestamp));
    }

    public void CancelTenantMigration(EventActor actor, Instant timestamp)
    {
        if (PendingMigrationTenantId is not { } target)
            return;

        PendingMigrationTenantId = null;
        PendingMigrationStagedAt = null;

        AddDomainEvent(new ApplicationUserTenantMigrationCanceledEvent(Id, target, actor, timestamp));
    }

    public void CompleteTenantMigration(string tenantId, EventActor actor, Instant timestamp)
    {
        PendingMigrationTenantId = null;
        PendingMigrationStagedAt = null;

        AddDomainEvent(new ApplicationUserTenantMigrationCompletedEvent(Id, tenantId, actor, timestamp));
    }

    public void StageProviderMigration(string targetProvider, EventActor actor, Instant timestamp)
    {
        var previous = PendingMigrationProviderId;
        if (previous == targetProvider)
            return;

        PendingMigrationProviderId = targetProvider;

        AddDomainEvent(new ApplicationUserProviderMigrationStagedEvent(Id, targetProvider, previous, actor, timestamp));
    }

    public void CancelProviderMigration(EventActor actor, Instant timestamp)
    {
        if (PendingMigrationProviderId is not { } target)
            return;

        PendingMigrationProviderId = null;

        AddDomainEvent(new ApplicationUserProviderMigrationCanceledEvent(Id, target, actor, timestamp));
    }

    public void CompleteProviderMigration(string provider, EventActor actor, Instant timestamp)
    {
        var from = LoginProvider;

        LoginProvider = provider;
        PendingMigrationProviderId = null;

        AddDomainEvent(new ApplicationUserProviderMigrationCompletedEvent(Id, from, provider, actor, timestamp));
    }

    /// <remarks>
    /// A staged provider migration would move the user off the account this creates, so it is canceled, and
    /// recorded as canceled, first.
    /// </remarks>
    public void ConvertToLocalAccount(EventActor actor, Instant timestamp)
    {
        CancelProviderMigration(actor, timestamp);

        var from = LoginProvider;

        LoginProvider = LoginProviders.Wayd;
        MustChangePassword = true;

        AddDomainEvent(new ApplicationUserConvertedToLocalAccountEvent(Id, from, actor, timestamp));
    }

    IReadOnlyCollection<DomainEvent> IEntity.DomainEvents => _domainEvents.AsReadOnly();

    IReadOnlyCollection<Action> IEntity.PostPersistenceActions => _postPersistenceActions.AsReadOnly();

    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void AddPostPersistenceAction(Action action) => _postPersistenceActions.Add(action);

    public void RemovePostPersistenceAction(Action action) => _postPersistenceActions.Remove(action);

    public void ClearPostPersistenceActions() => _postPersistenceActions.Clear();

    public void ExecutePostPersistenceActions()
    {
        foreach (var action in _postPersistenceActions)
        {
            action();
        }
        _postPersistenceActions.Clear();
    }
}