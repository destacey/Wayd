using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.PpmTeams.Dtos;

/// <summary>
/// Navigation DTO for a PPM team.
/// </summary>
public sealed record PpmTeamNavigationDto : IMapFrom<PpmTeam>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PpmTeam, PpmTeamNavigationDto>()
            .Map(dest => dest.Code, src => src.Code.Value);
    }

    public static PpmTeamNavigationDto From(PpmTeam team)
    {
        return new PpmTeamNavigationDto
        {
            Id = team.Id,
            Key = team.Key,
            Code = team.Code.Value,
            Name = team.Name
        };
    }
}
