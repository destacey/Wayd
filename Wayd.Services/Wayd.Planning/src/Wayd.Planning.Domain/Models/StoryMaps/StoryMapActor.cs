namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// The person acting on a story map, with what the map needs to decide what they may do.
/// </summary>
/// <param name="EmployeeId">The employee the caller acts as. Owners and editors are employees.</param>
/// <param name="IsAdministrator">Holds the Story Maps Administer permission, which lets them manage any map.</param>
/// <param name="TeamIds">
/// The map's editor teams the employee belongs to, as <c>IsTeamMemberQuery</c> decides membership. Teams the map
/// does not name may be left out, since only editor teams grant access.
/// </param>
public sealed record StoryMapActor(Guid EmployeeId, bool IsAdministrator, IReadOnlySet<Guid> TeamIds);
