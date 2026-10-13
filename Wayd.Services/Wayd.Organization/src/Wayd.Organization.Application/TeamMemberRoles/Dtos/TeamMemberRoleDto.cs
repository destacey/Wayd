using Mapster;

namespace Wayd.Organization.Application.TeamMemberRoles.Dtos;

public sealed record TeamMemberRoleDto : IMapFrom<TeamMemberRole>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Whether time allocated to a team in this role adds to the team's delivery capacity.</summary>
    public bool CountsTowardDeliveryCapacity { get; set; }
}
