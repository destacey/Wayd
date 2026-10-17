using Wayd.Common.Application.Dtos;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.Visions.Dtos;

public sealed record VisionDto : IMapFrom<Vision>
{
    /// <summary>
    /// The unique identifier of the vision.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The vision's unique, human-readable number; an alternate key to <c>id</c>.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// A concise statement describing the vision of the organization.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// The current lifecycle state of the vision: Proposed, Active, or Archived.
    /// </summary>
    public required SimpleNavigationDto State { get; set; }

    /// <summary>
    /// When the vision became active and started guiding the organization; null while it is proposed.
    /// </summary>
    public Instant? Start { get; set; }

    /// <summary>
    /// When the vision was archived; null until it is archived.
    /// </summary>
    public Instant? End { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Vision, VisionDto>()
            .Map(dest => dest.State, src => SimpleNavigationDto.FromEnum(src.State))
            .Map(dest => dest.Start, src => src.Dates == null ? null : (Instant?)src.Dates.Start)
            .Map(dest => dest.End, src => src.Dates == null ? null : src.Dates.End);
    }
}
