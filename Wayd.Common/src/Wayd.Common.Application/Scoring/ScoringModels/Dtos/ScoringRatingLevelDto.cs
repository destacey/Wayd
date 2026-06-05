using Wayd.Common.Domain.Scoring;

using Mapster;
namespace Wayd.Common.Application.Scoring.ScoringModels.Dtos;

public sealed record ScoringRatingLevelDto : IMapFrom<ScoringRatingLevel>
{
    public Guid Id { get; set; }
    public required string Label { get; set; }
    public decimal Value { get; set; }
    public int Order { get; set; }
}
