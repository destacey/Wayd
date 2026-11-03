using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Extensions;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

/// <summary>
/// The full Story Map: its access, goals (each with steps and tasks), swim lanes, and personas.
/// </summary>
public sealed record StoryMapDetailsDto : IMapFrom<StoryMap>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }

    /// <summary>
    /// Public: anyone with the Story Maps View permission can see the map. Private: only its owners and editors.
    /// </summary>
    public Visibility Visibility { get; set; }

    /// <summary>
    /// Who can change a public map's content: anyone who can view it, or only its owners and editors. A private map
    /// admits only its owners and editors, so there it has no effect.
    /// </summary>
    public StoryMapEditAccess EditAccess { get; set; }

    /// <summary>
    /// The employees who own the map: its details, lifecycle and access.
    /// </summary>
    public List<EmployeeNavigationDto> Owners { get; set; } = [];

    /// <summary>
    /// The employees invited to edit the map.
    /// </summary>
    public List<EmployeeNavigationDto> Editors { get; set; } = [];

    /// <summary>
    /// The teams whose current members, and the members of their current team of teams, are invited to edit the map.
    /// </summary>
    public List<PlanningTeamNavigationDto> EditorTeams { get; set; } = [];

    /// <summary>
    /// Whether the caller manages the map, as an owner or a Story Maps administrator. Always false for a read-only
    /// token or an account with no linked employee.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Whether the caller can change the map's content. Says nothing about whether the map is archived. Always false
    /// for a read-only token or an account with no linked employee.
    /// </summary>
    public bool CanEdit { get; set; }

    public required IReadOnlyList<StoryMapGoalDto> Goals { get; set; }
    public required IReadOnlyList<StoryMapSwimLaneDto> SwimLanes { get; set; }
    public required IReadOnlyList<StoryMapPersonaDto> Personas { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<StoryMap, StoryMapDetailsDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName())
            .Map(dest => dest.Owners, src => src.Owners.OrderBy(o => o.Employee!.Name.FirstName).ThenBy(o => o.Employee!.Name.LastName).Select(o => EmployeeNavigationDto.From(o.Employee!)))
            .Map(dest => dest.Editors, src => src.Editors.OrderBy(e => e.Employee!.Name.FirstName).ThenBy(e => e.Employee!.Name.LastName).Select(e => EmployeeNavigationDto.From(e.Employee!)))
            .Map(dest => dest.EditorTeams, src => src.EditorTeams.OrderBy(t => t.Team!.Name).Select(t => PlanningTeamNavigationDto.FromPlanningTeam(t.Team!)))
            .Ignore(dest => dest.CanManage)
            .Ignore(dest => dest.CanEdit);
    }
}
