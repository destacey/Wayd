using System.ComponentModel.DataAnnotations;
using Mapster;
using NodaTime;

namespace Wayd.Organization.Application.TeamsOfTeams.Dtos;

/// <summary>
/// Represents the operating model for a team of teams.
/// </summary>
public sealed record TeamOfTeamsOperatingModelDetailsDto : IMapFrom<TeamOfTeamsOperatingModel>
{
    /// <summary>
    /// The identifier of the operating model.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// The identifier of the team of teams.
    /// </summary>
    [Required]
    public Guid TeamId { get; set; }

    /// <summary>
    /// The start date of the operating model.
    /// </summary>
    [Required]
    public LocalDate Start { get; set; }

    /// <summary>
    /// The end date of the operating model. Null indicates the model is current.
    /// </summary>
    public LocalDate? End { get; set; }

    /// <summary>
    /// The IANA id of the time zone the team of teams' own rollups count days in.
    /// </summary>
    [Required]
    public required string TimeZone { get; set; }

    /// <summary>
    /// Indicates whether this operating model is current (has no end date).
    /// </summary>
    [Required]
    public bool IsCurrent { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        // TeamId is set by the query handlers, which load the model through its team of teams.
        config.NewConfig<TeamOfTeamsOperatingModel, TeamOfTeamsOperatingModelDetailsDto>()
            .Map(dest => dest.Start, src => src.DateRange.Start)
            .Map(dest => dest.End, src => src.DateRange.End)
            .Map(dest => dest.IsCurrent, src => src.IsCurrent);
    }
}
