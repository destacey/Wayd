using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Tests.Sut.Data;

public sealed class BaseEntityTests
{
    private sealed class TestEntity : BaseEntity<int>
    {
        public TestEntity(int id)
        {
            Id = id;
        }
    }

    private sealed record TestDomainEvent : DomainEvent
    {
        public string Name { get; }

        public TestDomainEvent(string name)
            : base(EventActor.System)
        {
            Name = name;
            Timestamp = NodaTime.SystemClock.Instance.GetCurrentInstant();
        }
    }

    [Fact]
    public void AddDomainEvent_ShouldAddEvent()
    {
        // Arrange
        var entity = new TestEntity(1);
        var ev = new TestDomainEvent("ev1");

        // Act
        entity.AddDomainEvent(ev);

        // Assert
        entity.DomainEvents.Should().ContainSingle().Which.Should().Be(ev);
    }

    [Fact]
    public void RemoveDomainEvent_ShouldRemoveEvent()
    {
        // Arrange
        var entity = new TestEntity(1);
        var ev = new TestDomainEvent("ev1");
        entity.AddDomainEvent(ev);

        // Act
        entity.RemoveDomainEvent(ev);

        // Assert
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_ShouldClearAll()
    {
        // Arrange
        var entity = new TestEntity(1);
        entity.AddDomainEvent(new TestDomainEvent("a"));
        entity.AddDomainEvent(new TestDomainEvent("b"));

        // Act
        entity.ClearDomainEvents();

        // Assert
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddPostPersistenceAction_ShouldAddAction()
    {
        // Arrange
        var entity = new TestEntity(1);
        var executed = false;
        Action act = () => executed = true;

        // Act
        entity.AddPostPersistenceAction(act);

        // Assert
        entity.PostPersistenceActions.Should().ContainSingle();
        executed.Should().BeFalse();
    }

    [Fact]
    public void RemovePostPersistenceAction_ShouldRemoveAction()
    {
        // Arrange
        var entity = new TestEntity(1);
        var executed = false;
        Action act = () => executed = true;
        entity.AddPostPersistenceAction(act);

        // Act
        entity.RemovePostPersistenceAction(act);

        // Assert
        entity.PostPersistenceActions.Should().BeEmpty();
        executed.Should().BeFalse();
    }

    [Fact]
    public void ExecutePostPersistenceActions_ShouldExecuteAndClear()
    {
        // Arrange
        var entity = new TestEntity(1);
        var counter = 0;
        entity.AddPostPersistenceAction(() => counter++);
        entity.AddPostPersistenceAction(() => counter += 2);

        // Act
        entity.ExecutePostPersistenceActions();

        // Assert
        counter.Should().Be(3);
        entity.PostPersistenceActions.Should().BeEmpty();
    }
}
