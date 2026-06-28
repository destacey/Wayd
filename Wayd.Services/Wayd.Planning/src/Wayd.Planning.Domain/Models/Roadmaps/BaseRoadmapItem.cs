using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Domain.Models.Roadmaps;

public abstract class BaseRoadmapItem : BaseAuditableEntity
{
    /// <summary>
    /// The Roadmap Id.
    /// </summary>
    public Guid RoadmapId { get; protected init; }

    /// <summary>
    /// The name of the Roadmap.
    /// </summary>
    public string Name
    {
        get;
        protected set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The description of the Roadmap.
    /// </summary>
    public string? Description
    {
        get;
        protected set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The type of the Roadmap Item.
    /// </summary>
    public RoadmapItemType Type { get; protected init; }

    /// <summary>
    /// The parent Roadmap Activity Id. This is used to connect Roadmap Items together.
    /// </summary>
    public Guid? ParentId { get; protected set; }

    /// <summary>
    /// The parent Roadmap Activity. This is used to connect Roadmap Items together.
    /// </summary>
    public RoadmapActivity? Parent { get; protected set; }

    /// <summary>
    /// The color of the Roadmap. This is used to display the Roadmap in the UI.
    /// </summary>
    public string? Color { get; protected set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>
    /// Change the parent Roadmap Activity from the Roadmap Item.
    /// </summary>
    internal Result ChangeParent(RoadmapActivity? newParentActivity)
    {
        if (ParentId == newParentActivity?.Id)
            return Result.Failure("Unable to change the parent because the new parent is the same as the current parent.");

        if (Id == newParentActivity?.Id)
            return Result.Failure("Unable to make the Roadmap Item a child of itself.");

        if (ParentId.HasValue)
        {
            if (Parent is null)
                return Result.Failure("Unable to change the parent because the current parent data has not been loaded.");

            Parent.RemoveChild(Id);
        }

        ParentId = newParentActivity?.Id;
        Parent = newParentActivity;
        newParentActivity?.AddChild(this);

        return Result.Success();
    }

    /// <summary>
    /// Sets the parent Roadmap Activity directly. Used for copying items where IDs are not yet assigned.
    /// </summary>
    /// <param name="newParentActivity">The new parent activity to assign.</param>
    internal void SetParentDirect(RoadmapActivity? newParentActivity)
    {
        ParentId = newParentActivity?.Id;
        Parent = newParentActivity;
        newParentActivity?.AddChild(this);
    }

    /// <summary>
    /// Links this item's parent navigation to the given Activity without modifying the parent's
    /// children collection. Used when the item is created already inside the parent's children
    /// (e.g. <see cref="RoadmapActivity.CreateChildActivity" />) so the in-memory parent chain is
    /// established for date rollup, independent of EF relationship fixup.
    /// </summary>
    /// <param name="parentActivity">The parent activity to link to.</param>
    internal void LinkParent(RoadmapActivity parentActivity)
    {
        ParentId = parentActivity.Id;
        Parent = parentActivity;
    }
}
