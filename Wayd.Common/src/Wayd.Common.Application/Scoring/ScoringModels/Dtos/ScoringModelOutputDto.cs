using Mapster;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Scoring.ScoringModels.Dtos;

public sealed record ScoringModelOutputDto : IMapFrom<ScoringModelOutput>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Token { get; set; }
    public required string Formula { get; set; }
    public bool IsPrimary { get; set; }
    public int Order { get; set; }
}
