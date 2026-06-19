using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Scoring;

using Mapster;
namespace Wayd.Common.Application.Scoring.ScoringModels.Dtos;

public sealed record ScoringModelDetailsDto : IMapFrom<ScoringModel>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required SimpleNavigationDto State { get; set; }
    public required List<ScoringModelCriterionDto> Criteria { get; set; }
    public required List<ScoringScaleDto> Scales { get; set; }
    public required List<ScoringModelOutputDto> Outputs { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ScoringModel, ScoringModelDetailsDto>()
            .Map(dest => dest.State, src => SimpleNavigationDto.FromEnum(src.State))
            .Map(dest => dest.Criteria, src => src.Criteria.OrderBy(c => c.Order))
            .Map(dest => dest.Scales, src => src.Scales.OrderBy(s => s.Order))
            .Map(dest => dest.Outputs, src => src.Outputs.OrderBy(o => o.Order));
    }
}
