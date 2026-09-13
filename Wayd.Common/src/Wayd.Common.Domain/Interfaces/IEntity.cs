namespace Wayd.Common.Domain.Interfaces;

public interface IEntity
{
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }
    IReadOnlyCollection<Action> PostPersistenceActions { get; }

    void AddDomainEvent(DomainEvent domainEvent);
    void RemoveDomainEvent(DomainEvent domainEvent);
    void ClearDomainEvents();

    void AddPostPersistenceAction(Action action);
    void RemovePostPersistenceAction(Action action);
    void ExecutePostPersistenceActions();
    void ClearPostPersistenceActions();
}

public interface IEntity<TId> : IEntity
{
    TId Id { get; }
}
