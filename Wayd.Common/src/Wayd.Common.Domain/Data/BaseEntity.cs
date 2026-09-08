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

    /// <summary>
    /// Adds a domain event, dropping any event of the same type still pending on this entity.
    /// </summary>
    /// <remarks>
    /// For events whose payload is the <em>state after the change</em> rather than a description of the
    /// change itself. The last one raised in a transaction already describes the net result, so the ones
    /// before it describe intermediate states that were never committed: replacing a project's role list
    /// twice in one request would otherwise record two changes, the first of them a set that never
    /// existed as a fact.
    /// <para>
    /// Use <see cref="AddDomainEvent"/> instead for an event that records <em>movement</em>, where each
    /// occurrence is its own fact. A status transition is the clear case — an import walking a record
    /// through several statuses in one transaction is several transitions, each with its own history row
    /// and sequence number, and superseding them would lose the path taken. The same goes for anything
    /// appended to a ledger, such as a health check or a score.
    /// </para>
    /// </remarks>
    public void AddSupersedingDomainEvent<TEvent>(TEvent domainEvent) where TEvent : DomainEvent
    {
        _domainEvents.RemoveAll(existing => existing is TEvent);
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