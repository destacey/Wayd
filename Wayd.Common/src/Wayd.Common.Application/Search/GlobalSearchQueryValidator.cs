namespace Wayd.Common.Application.Search;

public sealed class GlobalSearchQueryValidator : CustomValidator<GlobalSearchQuery>
{
    public GlobalSearchQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.SearchTerm)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(256);

        RuleFor(q => q.MaxResultsPerCategory)
            .InclusiveBetween(1, 25);
    }
}
