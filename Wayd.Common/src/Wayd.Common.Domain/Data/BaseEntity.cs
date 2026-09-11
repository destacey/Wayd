using System.ComponentModel.DataAnnotations.Schema;

namespace Wayd.Common.Domain.Data;

public abstract class BaseEntity<TId> : IEntity<TId>
{
    /// <summary>
    /// Unique identifier for this entity.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    private readonly List<DomainEvent> _domainEvents = [];
    private readonly List<Action> _postPersistenceActions = [];

    [NotMapped]
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    [NotMapped]
    public IReadOnlyCollection<Action> PostPersistenceActions => _postPersistenceActions.AsReadOnly();

    public void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void AddPostPersistenceAction(Action action)
    {
        _postPersistenceActions.Add(action);
    }

    public void RemovePostPersistenceAction(Action action)
    {
        _postPersistenceActions.Remove(action);
    }

    public void ExecutePostPersistenceActions()
    {
        foreach (var action in _postPersistenceActions)
        {
            action();
        }
        _postPersistenceActions.Clear();
    }
}

public abstract class BaseEntity : BaseEntity<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.CreateVersion7();
    }
}