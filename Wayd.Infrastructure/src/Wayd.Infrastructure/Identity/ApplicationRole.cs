using Microsoft.AspNetCore.Identity;
using Wayd.Common.Domain.Events.Identity;
using NodaTime;

namespace Wayd.Infrastructure.Identity;

/// <remarks>
/// Implements <see cref="IEntity"/> itself for the same reason <see cref="ApplicationUser"/> does, and with the same
/// rule: a caller whose <see cref="RoleManager{TRole}"/> call fails without saving clears the events it raised.
/// </remarks>
public class ApplicationRole : IdentityRole, IEntity
{
    private readonly List<DomainEvent> _domainEvents = [];
    private readonly List<Action> _postPersistenceActions = [];

    public ApplicationRole(string name, string? description = null)
        : base(name)
    {
        Description = NormalizeDescription(description);
        NormalizedName = name.Trim().ToUpperInvariant();
    }

    public string? Description { get; set; }

    public ICollection<ApplicationUserRole> UserRoles { get; set; } = [];

    // A form sends an empty description where none was given; stored as that, it would read as a change from null.
    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public void RecordCreation(EventActor actor, Instant timestamp) =>
        AddDomainEvent(new ApplicationRoleCreatedEvent(Id, Name!, actor, timestamp));

    public void Update(string name, string? description, EventActor actor, Instant timestamp)
    {
        var before = new ApplicationRoleDetails(Name!, Description);

        Name = name.Trim();
        NormalizedName = name.Trim().ToUpperInvariant();
        Description = NormalizeDescription(description);

        if (before != new ApplicationRoleDetails(Name, Description))
        {
            AddDomainEvent(new ApplicationRoleDetailsUpdatedEvent(Id, Name, Description, before, actor, timestamp));
        }
    }

    /// <summary>
    /// Records a change to the permissions the role grants, which are role claims rather than state on this
    /// entity. Raises nothing when the two sets hold the same permissions.
    /// </summary>
    public void RecordPermissionsChange(IEnumerable<string> permissionsBefore, IEnumerable<string> permissionsAfter, EventActor actor, Instant timestamp)
    {
        var before = permissionsBefore.ToHashSet();
        var after = permissionsAfter.ToHashSet();

        string[] added = [.. after.Except(before).Order()];
        string[] removed = [.. before.Except(after).Order()];
        if (added.Length == 0 && removed.Length == 0)
            return;

        AddDomainEvent(new ApplicationRolePermissionsChangedEvent(Id, added, removed, [.. after.Order()], actor, timestamp));
    }

    public void RecordDeletion(EventActor actor, Instant timestamp) =>
        AddDomainEvent(new ApplicationRoleDeletedEventV2(Id, Name!, actor, timestamp));

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
