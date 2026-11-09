using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using OneOf;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

public sealed record CreateRoadmapActivityRequest : CreateRoadmapItemRequest
{
    /// <summary>
    /// The Activity start date.
    /// </summary>
    public LocalDate Start { get; set; }

    /// <summary>
    /// The Activity end date.
    /// </summary>
    public LocalDate End { get; set; }

    /// <summary>
    /// The Activity's 1-based position among its parent's child Activities, or among the root Activities
    /// (optional). Omit it to place the Activity last. Timeboxes and milestones are not counted: they always
    /// follow the Activities. A position more than one past the last Activity is rejected.
    /// </summary>
    public int? Order { get; set; }

    /// <summary>
    /// The create command for this request.
    /// </summary>
    public CreateRoadmapItemCommand ToCreateRoadmapItemCommand()
    {
        return new CreateRoadmapItemCommand(RoadmapId, OneOf<IUpsertRoadmapActivity, IUpsertRoadmapMilestone, IUpsertRoadmapTimebox>.FromT0(new UpsertRoadmapActivityAdapter(this)), Order);
    }
}

public sealed class CreateRoadmapActivityRequestValidator : CustomValidator<CreateRoadmapActivityRequest>
{
    public CreateRoadmapActivityRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new CreateRoadmapItemRequestValidator());

        RuleFor(t => t.Start)
            .NotNull();

        RuleFor(t => t.End)
            .NotNull()
            .Must((membership, end) => membership.Start <= end)
                .WithMessage("End date must be greater than or equal to start date");

        RuleFor(t => t.Order)
            .GreaterThan(0)
            .When(t => t.Order.HasValue);
    }
}
