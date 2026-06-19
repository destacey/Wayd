using Wayd.Common.Application.Dtos;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.Teams.Dtos;

public record TeamMemberDto
{
    public required TeamMemberEmployeeDto Employee { get; set; }
    public required TeamNavigationDto Team { get; set; }
    public required IReadOnlyList<NavigationDto> Roles { get; set; }
}

public record TeamMemberEmployeeDto
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? JobTitle { get; set; }
}
