using Wayd.Common.Application.Dtos;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.Strategies.Dtos;

public sealed record StrategyListDto : IMapFrom<Strategy>
{
    /// <summary>
    /// The unique identifier of the strategy.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The strategy's unique, human-readable number; an alternate key to <c>id</c>.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The concise statement describing the strategy and its purpose or focus area.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The current status of the strategy (e.g., Draft, Active, Completed, Archived).
    /// </summary>
    public required SimpleNavigationDto Status { get; set; }

    /// <summary>
    /// The date when the strategy was initiated or became active.
    /// </summary>
    public LocalDate? Start { get; set; }

    /// <summary>
    /// The date when the strategy was completed, archived, or ended.
    /// </summary>
    public LocalDate? End { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Strategy, StrategyListDto>()
            .Map(dest => dest.Status, src => SimpleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.Start, src => src.Dates == null ? null : (LocalDate?)src.Dates.Start)
            .Map(dest => dest.End, src => src.Dates == null ? null : src.Dates.End);
    }
}
