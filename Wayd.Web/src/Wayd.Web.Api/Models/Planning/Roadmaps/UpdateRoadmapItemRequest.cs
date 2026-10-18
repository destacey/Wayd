using System.Text.Json.Serialization;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Roadmaps.Dtos;

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

    /// <summary>
    /// Creates the update request matching the item's type from its current details, for applying a patch to.
    /// </summary>
    /// <exception cref="ArgumentException">The details are not an activity, milestone or timebox.</exception>
    public static UpdateRoadmapItemRequest FromDto(RoadmapItemDetailsDto dto) => dto switch
    {
        RoadmapActivityDetailsDto activity => UpdateRoadmapActivityRequest.FromDto(activity),
        RoadmapMilestoneDetailsDto milestone => UpdateRoadmapMilestoneRequest.FromDto(milestone),
        RoadmapTimeboxDetailsDto timebox => UpdateRoadmapTimeboxRequest.FromDto(timebox),
        _ => throw new ArgumentException("Invalid roadmap item type", nameof(dto))
    };

    /// <summary>
    /// Builds the update command for this item's type.
    /// </summary>
    public abstract UpdateRoadmapItemCommand ToUpdateRoadmapItemCommand();
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
