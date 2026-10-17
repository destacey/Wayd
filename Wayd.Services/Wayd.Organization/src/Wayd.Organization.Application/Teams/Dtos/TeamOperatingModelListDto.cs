using System.ComponentModel.DataAnnotations;
using Mapster;
using Wayd.Common.Domain.Enums.Organization;

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
    /// The length in weeks of a Kanban team's reporting periods, or null for no cadence.
    /// </summary>
    public int? CadenceWeeks { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<TeamOperatingModel, TeamOperatingModelListDto>()
            .Map(dest => dest.CadenceWeeks, src => src.Cadence == null ? (int?)null : src.Cadence.Weeks);
    }
}
