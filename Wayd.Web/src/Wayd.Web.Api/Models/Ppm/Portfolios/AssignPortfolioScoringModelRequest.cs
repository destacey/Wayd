namespace Wayd.Web.Api.Models.Ppm.Portfolios;

public sealed record AssignPortfolioScoringModelRequest
{
    public Guid ScoringModelId { get; set; }
}

public sealed class AssignPortfolioScoringModelRequestValidator : CustomValidator<AssignPortfolioScoringModelRequest>
{
    public AssignPortfolioScoringModelRequestValidator()
    {
        RuleFor(r => r.ScoringModelId).NotEmpty();
    }
}
