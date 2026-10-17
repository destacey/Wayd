using Wayd.Common.Application.Dtos;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Dtos;

public sealed record StrategicThemeListDto : IMapFrom<StrategicTheme>
{
    /// <summary>
    /// The unique identifier of the strategic theme.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The strategic theme's unique, human-readable number; an alternate key to <c>id</c>.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the strategic theme, highlighting its focus or priority.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The current lifecycle state of the strategic theme.
    /// </summary>
    public required SimpleNavigationDto State { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<StrategicTheme, StrategicThemeListDto>()
            .Map(dest => dest.State, src => SimpleNavigationDto.FromEnum(src.State));
    }
}
