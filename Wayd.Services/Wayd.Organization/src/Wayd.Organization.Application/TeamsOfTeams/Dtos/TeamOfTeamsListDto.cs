using System.ComponentModel.DataAnnotations;
using Mapster;
using Wayd.Organization.Application.Models;
using NodaTime;

namespace Wayd.Organization.Application.TeamsOfTeams.Dtos;

public class TeamOfTeamsListDto : IMapFrom<BaseTeam>
{
    /// <summary>The team of teams' unique identifier.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The team of teams' numeric key, an alternate to its <c>id</c>. Endpoints differ in which of the two they accept; a uuid parameter takes the id.</summary>
    [Required]
    public int Key { get; set; }

    /// <summary>
    /// The name of the team of teams.
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>The team of teams' short code, 2 to 10 characters, unique across teams and teams of teams.</summary>
    [Required]
    public required string Code { get; set; }

    /// <summary>The team type's display name: "Team" or "Team of Teams".</summary>
    [Required]
    public required string Type { get; set; }

    /// <summary>
    /// Whether the team of teams is active.
    /// </summary>
    [Required]
    public bool IsActive { get; set; }

    public TeamNavigationDto? TeamOfTeams { get; set; }

    /// <summary>
    /// Create a TypeAdapterConfig configured to map BaseTeam -> TeamOfTeamsListDto using the provided
    /// asOf date to select the appropriate parent membership. Callers can pass this config to
    /// ProjectToType to perform an EF-friendly projection that uses a captured constant date.
    /// </summary>
    public static TypeAdapterConfig CreateTypeAdapterConfig(LocalDate asOf)
    {
        var cfg = new TypeAdapterConfig();

        cfg.NewConfig<BaseTeam, TeamOfTeamsListDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Key, src => src.Key)
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Code, src => src.Code.Value)
            .Map(dest => dest.Type, src => src.Type.GetDisplayName())
            .Map(dest => dest.IsActive, src => src.IsActive)
            .Map(dest => dest.TeamOfTeams,
                 src => src.ParentMemberships
                            .Where(m => m.DateRange.Start <= asOf && (m.DateRange.End == null || m.DateRange.End >= asOf))
                            .Select(m => m.Target)
                            .FirstOrDefault());

        return cfg;
    }
}
