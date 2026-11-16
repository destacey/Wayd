using Wayd.Planning.Domain.Interfaces.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Validators;

public sealed class IUpsertRoadmapMilestoneDateValidator : CustomValidator<IUpsertRoadmapMilestoneDate>
{
    public IUpsertRoadmapMilestoneDateValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Date)
            .NotEmpty();
    }
}
