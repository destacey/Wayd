using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Commands;

namespace Wayd.Web.Api.Models.Ppm.Projects;

public sealed record RecordProjectScoreRequest
{
    public required List<CriterionRatingRequest> Ratings { get; set; } = [];

    public sealed record CriterionRatingRequest
    {
        public Guid CriterionId { get; set; }
        public decimal? Value { get; set; }
        public Guid? RatingLevelId { get; set; }
    }

    public RecordProjectScoreCommand ToCommand(Guid projectId)
        => new(
            projectId,
            Ratings
                .Select(r => new RecordProjectScoreCommand.CriterionRatingInput(r.CriterionId, r.Value, r.RatingLevelId))
                .ToList());
}

public sealed class RecordProjectScoreRequestValidator : CustomValidator<RecordProjectScoreRequest>
{
    public RecordProjectScoreRequestValidator()
    {
        RuleFor(r => r.Ratings)
            .NotEmpty()
            .WithMessage("At least one criterion rating is required.");

        RuleForEach(r => r.Ratings).ChildRules(rating =>
        {
            rating.RuleFor(r => r.CriterionId).NotEmpty();
        });
    }
}
