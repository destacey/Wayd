using Wayd.Common.Domain.Scoring;

using Mapster;
namespace Wayd.Common.Application.Scoring.ScoringModels.Dtos;

public sealed record ScoringModelCriterionDto : IMapFrom<ScoringModelCriterion>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Token { get; set; }
    public string? Description { get; set; }
    public decimal? Weight { get; set; }
    public Guid? ScaleId { get; set; }
    public int Order { get; set; }
}
