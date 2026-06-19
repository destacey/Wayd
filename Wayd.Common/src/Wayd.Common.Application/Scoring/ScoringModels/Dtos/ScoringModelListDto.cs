using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Scoring;

using Mapster;
namespace Wayd.Common.Application.Scoring.ScoringModels.Dtos;

public sealed record ScoringModelListDto : IMapFrom<ScoringModel>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required SimpleNavigationDto State { get; set; }
    public int CriterionCount { get; set; }
    public int ScaleCount { get; set; }
    public int OutputCount { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ScoringModel, ScoringModelListDto>()
            .Map(dest => dest.State, src => SimpleNavigationDto.FromEnum(src.State))
            .Map(dest => dest.CriterionCount, src => src.Criteria.Count)
            .Map(dest => dest.ScaleCount, src => src.Scales.Count)
            .Map(dest => dest.OutputCount, src => src.Outputs.Count);
    }
}
