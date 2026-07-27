using Wayd.Planning.Domain.Interfaces.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Validators;

public class IUpsertRoadmapItemValidator : CustomValidator<IUpsertRoadmapItem>
{
    public IUpsertRoadmapItemValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

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
