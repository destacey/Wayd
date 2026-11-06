using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Tagging;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One label in one product category — <c>ios</c> in Platform, <c>pci-scope</c> in Compliance — or a free tag in no
/// category, such as "migration wave 2".
/// </summary>
/// <remarks>
/// Descriptive only. Nothing in the domain branches on a tag — behaviour comes from the product's type — so adding
/// one can never change what a node is allowed to do. A tag in a category changes only through
/// <see cref="ProductTagCategory"/>.
/// </remarks>
public sealed class ProductTag : TagBase
{
    private ProductTag() { }

    internal ProductTag(Guid categoryId, string name, string? description)
        : base(categoryId, name, description)
    {
    }

    private ProductTag(string normalizedName)
        : base(normalizedName)
    {
    }

    /// <summary>
    /// Creates a free tag from a name someone typed while tagging. The caller checks that no free tag already has
    /// the name, and applies that one instead; the database refuses a duplicate regardless.
    /// </summary>
    public static Result<ProductTag> CreateFree(string name, EventActor actor, Instant timestamp)
    {
        var validated = ValidateFreeName(name);
        if (validated.IsFailure)
            return Result.Failure<ProductTag>(validated.Error);

        var tag = new ProductTag(validated.Value);
        tag.RaiseFreeTagCreated(actor, timestamp);

        return tag;
    }

    protected override DomainEvent FreeTagCreatedEvent(EventActor actor, Instant timestamp) =>
        new ProductFreeTagCreatedEvent(Id, Name, actor, timestamp);

    protected override DomainEvent FreeTagDetailsUpdatedEvent(string previousName, string? previousDescription, EventActor actor, Instant timestamp) =>
        new ProductFreeTagDetailsUpdatedEvent(Id, Name, Description, previousName, previousDescription, actor, timestamp);

    protected override DomainEvent FreeTagDeletedEvent(Guid? mergedIntoTagId, EventActor actor, Instant timestamp) =>
        new ProductFreeTagDeletedEvent(Id, Name, mergedIntoTagId, actor, timestamp);
}
