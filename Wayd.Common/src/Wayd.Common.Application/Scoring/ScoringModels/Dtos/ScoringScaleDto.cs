using Mapster;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Scoring.ScoringModels.Dtos;

public sealed record ScoringScaleDto : IMapFrom<ScoringScale>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int Order { get; set; }
    public required List<ScoringRatingLevelDto> Levels { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ScoringScale, ScoringScaleDto>()
            .Map(dest => dest.Levels, src => src.Levels.OrderBy(l => l.Order));
    }
}
