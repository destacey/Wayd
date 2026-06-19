using Wayd.Planning.Application.Roadmaps.Commands;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

public sealed record UpdateRoadmapActivityPlacementRequest
{
    public Guid RoadmapId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid ItemId { get; set; }
    public int Order { get; set; }

    public UpdateRoadmapActivityPlacementCommand ToUpdateRoadmapActivityPlacementCommand()
    {
        return new UpdateRoadmapActivityPlacementCommand(RoadmapId, ParentId, ItemId, Order);
    }
}

public sealed class UpdateRoadmapActivityPlacementRequestValidator : CustomValidator<UpdateRoadmapActivityPlacementRequest>
{
    public UpdateRoadmapActivityPlacementRequestValidator()
    {
        RuleFor(o => o.RoadmapId)
            .NotEmpty()
            .WithMessage("A valid roadmap id must be provided.");

        When(o => o.ParentId.HasValue, () =>
        {
            RuleFor(o => o.ParentId)
                .NotEmpty()
                .WithMessage("A valid parent activity id must be provided.");
        });

        RuleFor(o => o.ItemId)
            .NotEmpty()
            .WithMessage("A valid activity id must be provided.");

        RuleFor(o => o.Order)
            .GreaterThan(0)
            .WithMessage("Order must be greater than 0.");
    }
}
