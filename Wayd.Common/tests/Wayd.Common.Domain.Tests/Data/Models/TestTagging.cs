using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tagging;

namespace Wayd.Common.Domain.Tests.Data.Models;

/// <summary>
/// An event the tagging test doubles raise, naming which factory produced it.
/// </summary>
public sealed record TestTagEvent : DomainEvent
{
    public TestTagEvent(string kind, Guid? mergedIntoTagId = null)
        : base(EventActor.System, "1.0")
    {
        Kind = kind;
        MergedIntoTagId = mergedIntoTagId;
    }

    public string Kind { get; }
    public Guid? MergedIntoTagId { get; }

    public override ActivityCategory GetActivityCategory() => ActivityCategory.Updated;
}

/// <summary>
/// Concrete implementation of <see cref="TagBase"/> for testing purposes.
/// </summary>
public sealed class TestTag : TagBase
{
    private TestTag() { }

    internal TestTag(Guid categoryId, string name) : base(categoryId, name, null) { }

    private TestTag(string normalizedName) : base(normalizedName) { }

    public static Result<TestTag> CreateFree(string name)
    {
        var validated = ValidateFreeName(name);
        if (validated.IsFailure)
            return Result.Failure<TestTag>(validated.Error);

        var tag = new TestTag(validated.Value);
        tag.RaiseFreeTagCreated(EventActor.System, Instant.MinValue);

        return tag;
    }

    protected override DomainEvent FreeTagCreatedEvent(EventActor actor, Instant timestamp) => new TestTagEvent("FreeTagCreated");
    protected override DomainEvent FreeTagDetailsUpdatedEvent(string previousName, string? previousDescription, EventActor actor, Instant timestamp) => new TestTagEvent("FreeTagDetailsUpdated");
    protected override DomainEvent FreeTagDeletedEvent(Guid? mergedIntoTagId, EventActor actor, Instant timestamp) => new TestTagEvent("FreeTagDeleted", mergedIntoTagId);
}

/// <summary>
/// Concrete implementation of <see cref="TagCategoryBase{TTag}"/> for testing purposes.
/// </summary>
public sealed class TestTagCategory : TagCategoryBase<TestTag>
{
    private readonly List<TestTag> _tags = [];

    private TestTagCategory(bool allowsMany) : base("Axis", null, allowsMany, 1, isSystem: false) { }

    public static TestTagCategory Create(bool allowsMany, params string[] tagNames)
    {
        var category = new TestTagCategory(allowsMany);
        category._tags.AddRange(tagNames.Select(name => new TestTag(category.Id, name)));

        return category;
    }

    public TestTag this[string name] => _tags.Single(t => t.Name == name);

    protected override List<TestTag> TagList => _tags;
    protected override string TagInUseMessage => "in use";
    protected override string AxisInUseMessage => "axis in use";
    protected override TestTag CreateTag(string name, string? description) => new(Id, name);
    protected override DomainEvent CreatedEvent(EventActor actor, Instant timestamp) => new TestTagEvent("Created");
    protected override DomainEvent DetailsUpdatedEvent(string previousName, string? previousDescription, EventActor actor, Instant timestamp) => new TestTagEvent("DetailsUpdated");
    protected override DomainEvent ReorderedEvent(int fromOrder, EventActor actor, Instant timestamp) => new TestTagEvent("Reordered");
    protected override DomainEvent ActivatedEvent(EventActor actor, Instant timestamp) => new TestTagEvent("Activated");
    protected override DomainEvent DeactivatedEvent(EventActor actor, Instant timestamp) => new TestTagEvent("Deactivated");
    protected override DomainEvent DeletedEvent(EventActor actor, Instant timestamp) => new TestTagEvent("Deleted");
    protected override DomainEvent TagAddedEvent(TestTag tag, EventActor actor, Instant timestamp) => new TestTagEvent("TagAdded");
    protected override DomainEvent TagDetailsUpdatedEvent(TestTag tag, string previousName, string? previousDescription, EventActor actor, Instant timestamp) => new TestTagEvent("TagDetailsUpdated");
    protected override DomainEvent TagActivatedEvent(TestTag tag, EventActor actor, Instant timestamp) => new TestTagEvent("TagActivated");
    protected override DomainEvent TagDeactivatedEvent(TestTag tag, EventActor actor, Instant timestamp) => new TestTagEvent("TagDeactivated");
    protected override DomainEvent TagRemovedEvent(TestTag tag, EventActor actor, Instant timestamp) => new TestTagEvent("TagRemoved");
}

/// <summary>
/// One tag on one test record.
/// </summary>
public sealed record TestTagAssignment(Guid TagId, Guid? CategoryId) : ITagAssignment;
