using Mapster;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.TeamsOfTeams.Dtos;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

public class PlanningIntervalTeamResponse : IMapFrom<TeamListDto>, IMapFrom<TeamOfTeamsListDto>
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets the key.</summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the workspace.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets the code.</summary>
    public string Code { get; set; } = default!;

    /// <summary>Gets the team type.</summary>
    public string Type { get; set; } = default!;

    /// <summary>
    /// Indicates whether the organization is active or not.
    /// </summary>
    public bool IsActive { get; set; }

    public TeamNavigationDto? TeamOfTeams { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<TeamListDto, PlanningIntervalTeamResponse>();
        config.NewConfig<TeamOfTeamsListDto, PlanningIntervalTeamResponse>();
    }
}
