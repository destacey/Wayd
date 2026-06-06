using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

/// <summary>
/// A history-list view of a recorded project score: the headline values without the full rating
/// breakdown.
/// </summary>
public sealed record ProjectScoreSummaryDto : IMapFrom<ProjectScore>
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public Guid ScoringModelId { get; set; }
    public int ScoringModelKey { get; set; }
    public required string ScoringModelName { get; set; }
    public decimal PrimaryValue { get; set; }
    public Instant ScoredOn { get; set; }
    public EmployeeNavigationDto? ScoredBy { get; set; }
    public required List<ProjectScoreOutputDto> Outputs { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectScore, ProjectScoreSummaryDto>()
            .Map(dest => dest.ScoredBy, src => src.ScoredBy != null ? EmployeeNavigationDto.From(src.ScoredBy) : null)
            .Map(dest => dest.Outputs, src => src.Outputs.OrderBy(o => o.Order));
    }
}
