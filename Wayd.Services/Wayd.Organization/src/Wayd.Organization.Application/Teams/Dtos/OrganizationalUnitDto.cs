using Wayd.Common.Application.Dtos;

namespace Wayd.Organization.Application.Teams.Dtos;

/// <summary>
/// A team or team of teams in the functional organization chart, with the units beneath it.
/// </summary>
public sealed record OrganizationalUnitDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public required string Name { get; init; }
    public required string Code { get; init; }
    public required SimpleNavigationDto Type { get; init; }
    public int Level { get; init; }
    public required string Path { get; init; }
    public List<OrganizationalUnitDto>? Children { get; init; }
}
