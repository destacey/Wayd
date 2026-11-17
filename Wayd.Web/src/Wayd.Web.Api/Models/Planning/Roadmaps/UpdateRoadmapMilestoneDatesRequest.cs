using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using OneOf;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

public sealed record UpdateRoadmapMilestoneDatesRequest : UpdateRoadmapItemDatesRequest
{
    /// <summary>
    /// The Milestone date.
    /// </summary>
    public LocalDate Date { get; set; }

    public UpdateRoadmapItemDatesCommand ToUpdateRoadmapItemDatesCommand()
    {
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT1(new UpsertRoadmapMilestoneDateAdapter(this));

        return new UpdateRoadmapItemDatesCommand(RoadmapId, ItemId, dateUpdate);
    }
}

public sealed class UpdateRoadmapMilestoneDateRequestValidator : CustomValidator<UpdateRoadmapMilestoneDatesRequest>
{
    public UpdateRoadmapMilestoneDateRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Date)
            .NotEmpty();
    }
}
