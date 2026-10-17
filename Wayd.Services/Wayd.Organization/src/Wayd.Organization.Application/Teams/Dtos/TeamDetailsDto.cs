using System.ComponentModel.DataAnnotations;
using Mapster;
using Wayd.Organization.Application.Models;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Dtos;

public sealed record TeamDetailsDto : IMapFrom<Team>
{
    /// <summary>
    /// The identifier of the team.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// The key of the team.
    /// </summary>
    [Required]
    public int Key { get; set; }

    /// <summary>
    /// The name of the team.
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// The code of the team.
    /// </summary>
    [Required]
    public required string Code { get; set; }

    /// <summary>
    /// The description of the team.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The type of team.
    /// </summary>
    [Required]
    public required string Type { get; set; }

    /// <summary>
    /// The date for when the team became active.
    /// </summary>
    public LocalDate ActiveDate { get; set; }

    /// <summary>
    /// The date for when the team became inactive.
    /// </summary>
    public LocalDate? InactiveDate { get; set; }

    /// <summary>
    /// Indicates whether the team is active or not.  
    /// </summary>
    [Required]
    public bool IsActive { get; set; }

    /// <summary>
    /// The parent team of the team.
    /// </summary>
    public TeamNavigationDto? TeamOfTeams { get; set; }

    /// <summary>
    /// The current operating model of the team.
    /// </summary>
    [Required]
    public required TeamOperatingModelListDto OperatingModel { get; set; }

    /// <summary>
    /// Create a TypeAdapterConfig configured to map BaseTeam -> TeamDetailsDto using the provided
    /// asOf date to select the appropriate parent membership. Callers can pass this config to
    /// ProjectToType to perform an EF-friendly projection that uses a captured constant date.
    /// </summary>
    public static TypeAdapterConfig CreateTypeAdapterConfig(LocalDate asOf)
    {
        var cfg = new TypeAdapterConfig();

        new TeamOperatingModelListDto().ConfigureMapping(cfg);

        cfg.NewConfig<Team, TeamDetailsDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Key, src => src.Key)
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Code, src => src.Code.Value)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.Type, src => src.Type.GetDisplayName())
            .Map(dest => dest.ActiveDate, src => src.ActiveDate)
            .Map(dest => dest.InactiveDate, src => src.InactiveDate)
            .Map(dest => dest.IsActive, src => src.IsActive)
            .Map(dest => dest.TeamOfTeams,
                 src => src.ParentMemberships
                            .Where(m => m.DateRange.Start <= asOf && (m.DateRange.End == null || m.DateRange.End >= asOf))
                            .Select(m => m.Target)
                            .FirstOrDefault())
            .Map(dest => dest.OperatingModel, src => src.OperatingModels
                .Where(om => om.DateRange.Start <= asOf && (om.DateRange.End == null || om.DateRange.End >= asOf))
                .First());

        return cfg;
    }
}
