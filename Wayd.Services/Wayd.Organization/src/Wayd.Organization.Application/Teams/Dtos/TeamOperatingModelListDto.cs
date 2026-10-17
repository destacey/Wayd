using System.ComponentModel.DataAnnotations;
using Mapster;
using Wayd.Common.Domain.Enums.Organization;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Dtos;

/// <summary>
/// Represents the operating model for a team.
/// </summary>
public sealed record TeamOperatingModelListDto : IMapFrom<TeamOperatingModel>
{
    /// <summary>
    /// The identifier of the operating model.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// The methodology the team uses (e.g., Scrum, Kanban).
    /// </summary>
    [Required]
    public Methodology Methodology { get; set; }

    /// <summary>
    /// The sizing method the team uses (e.g., StoryPoints, Count).
    /// </summary>
    [Required]
    public SizingMethod SizingMethod { get; set; }

    /// <summary>
    /// The IANA id of the time zone the team's days are counted in, which decides what day it is for the team.
    /// </summary>
    [Required]
    public string TimeZone { get; set; } = null!;

    /// <summary>
    /// The length in weeks of a Kanban team's reporting periods, or null for no cadence.
    /// </summary>
    public int? CadenceWeeks { get; set; }

    /// <summary>
    /// The first day of one of the cadence's periods, which aligns the others; set exactly when
    /// <see cref="CadenceWeeks"/> is.
    /// </summary>
    public LocalDate? CadenceAnchor { get; set; }

    /// <summary>Flattens the model's optional cadence into its length and anchor.</summary>
    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<TeamOperatingModel, TeamOperatingModelListDto>()
            .Map(dest => dest.CadenceWeeks, src => src.Cadence == null ? (int?)null : src.Cadence.Weeks)
            .Map(dest => dest.CadenceAnchor, src => src.Cadence == null ? (LocalDate?)null : src.Cadence.Anchor);
    }
}
