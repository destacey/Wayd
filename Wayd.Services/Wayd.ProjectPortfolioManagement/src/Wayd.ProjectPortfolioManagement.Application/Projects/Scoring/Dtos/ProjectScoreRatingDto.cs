using Wayd.Common.Application.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

public sealed record ProjectScoreRatingDto : IMapFrom<ProjectScoreRating>
{
    public Guid CriterionId { get; set; }
    public required string CriterionName { get; set; }
    public required string CriterionToken { get; set; }
    public decimal RatingValue { get; set; }
    public Guid? RatingLevelId { get; set; }
    public string? RatingLevelLabel { get; set; }
    public int Order { get; set; }
}
