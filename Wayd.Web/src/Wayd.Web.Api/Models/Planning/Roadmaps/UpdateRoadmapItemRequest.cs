using System.Text.Json.Serialization;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

[JsonDerivedType(typeof(UpdateRoadmapActivityRequest), typeDiscriminator: "activity")]
[JsonDerivedType(typeof(UpdateRoadmapMilestoneRequest), typeDiscriminator: "milestone")]
[JsonDerivedType(typeof(UpdateRoadmapTimeboxRequest), typeDiscriminator: "timebox")]
public abstract record UpdateRoadmapItemRequest
{
    /// <summary>
    /// The Roadmap Id the Roadmap Item belongs to.
    /// </summary>
    public Guid RoadmapId { get; set; }

    /// <summary>
    /// The Roadmap Item Id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// The name of the Roadmap Item.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The description of the Roadmap Item.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The parent Roadmap Item Id. This is used to connect Roadmap Items together.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// The color of the Roadmap Item. This is used to display the Roadmap Item in the UI.
    /// </summary>
    public string? Color { get; set; }
}

public sealed class UpdateRoadmapItemRequestValidator : CustomValidator<UpdateRoadmapItemRequest>
{
    public UpdateRoadmapItemRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.RoadmapId)
            .NotEmpty();

        RuleFor(t => t.ItemId)
            .NotEmpty();

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Description)
            .MaximumLength(2048);

        When(x => x.ParentId.HasValue, () =>
        {
            RuleFor(x => x.ParentId)
                .NotEmpty();
        });

        When(x => x.Color != null, () => RuleFor(x => x.Color)
            .IsHexColor());
    }
}
