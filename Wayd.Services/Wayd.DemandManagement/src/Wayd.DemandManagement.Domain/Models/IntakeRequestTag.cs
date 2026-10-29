using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Tagging;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One label on one intake request tag axis — "Payments" on Area, "Enterprise" on Customer segment — or a free
/// tag on no axis, such as "follow up in Q3".
/// </summary>
/// <remarks>
/// A tag on an axis changes only through <see cref="IntakeRequestTagCategory"/>.
/// </remarks>
public sealed class IntakeRequestTag : TagBase
{
    private IntakeRequestTag() { }

    internal IntakeRequestTag(Guid categoryId, string name, string? description)
        : base(categoryId, name, description)
    {
    }

    private IntakeRequestTag(string normalizedName)
        : base(normalizedName)
    {
    }

    /// <summary>
    /// Creates a free tag from a name an agent typed while tagging. The caller checks that no free tag already has
    /// the name, and applies that one instead; the database refuses a duplicate regardless.
    /// </summary>
    public static Result<IntakeRequestTag> CreateFree(string name, EventActor actor, Instant timestamp)
    {
        var validated = ValidateFreeName(name);
        if (validated.IsFailure)
            return Result.Failure<IntakeRequestTag>(validated.Error);

        var tag = new IntakeRequestTag(validated.Value);
        tag.RaiseFreeTagCreated(actor, timestamp);

        return tag;
    }

    protected override DomainEvent FreeTagCreatedEvent(EventActor actor, Instant timestamp) =>
        new IntakeRequestFreeTagCreatedEvent(Id, Name, actor, timestamp);

    protected override DomainEvent FreeTagDetailsUpdatedEvent(string previousName, string? previousDescription, EventActor actor, Instant timestamp) =>
        new IntakeRequestFreeTagDetailsUpdatedEvent(Id, Name, Description, previousName, previousDescription, actor, timestamp);

    protected override DomainEvent FreeTagDeletedEvent(Guid? mergedIntoTagId, EventActor actor, Instant timestamp) =>
        new IntakeRequestFreeTagDeletedEvent(Id, Name, mergedIntoTagId, actor, timestamp);
}
