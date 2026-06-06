using Wayd.Common.Application.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

public sealed record ProjectScoreOutputDto : IMapFrom<ProjectScoreOutput>
{
    public required string Token { get; set; }
    public required string Name { get; set; }
    public decimal Value { get; set; }
    public bool IsPrimary { get; set; }
    public int Order { get; set; }
}
