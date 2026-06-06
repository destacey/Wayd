using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

/// <summary>
/// The full frozen snapshot of a recorded project score, including every criterion rating and computed
/// output value as they were at scoring time.
/// </summary>
public sealed record ProjectScoreDetailsDto : IMapFrom<ProjectScore>
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public long Sequence { get; set; }
    public Guid ScoringModelId { get; set; }
    public int ScoringModelKey { get; set; }
    public required string ScoringModelName { get; set; }
    public decimal PrimaryValue { get; set; }
    public Instant ScoredOn { get; set; }
    public EmployeeNavigationDto? ScoredBy { get; set; }
    public required List<ProjectScoreRatingDto> Ratings { get; set; }
    public required List<ProjectScoreOutputDto> Outputs { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectScore, ProjectScoreDetailsDto>()
            .Map(dest => dest.ScoredBy, src => src.ScoredBy != null ? EmployeeNavigationDto.From(src.ScoredBy) : null)
            .Map(dest => dest.Ratings, src => src.Ratings.OrderBy(r => r.Order))
            .Map(dest => dest.Outputs, src => src.Outputs.OrderBy(o => o.Order));
    }
}
