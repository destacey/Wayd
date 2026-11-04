namespace Wayd.Planning.Domain.Models.Roadmaps;

/// <summary>
/// The person acting on a roadmap, with what the roadmap needs to decide what they may do.
/// </summary>
/// <param name="EmployeeId">The employee the caller acts as. Owners are employees.</param>
/// <param name="IsAdministrator">Holds the Roadmaps Administer permission, which lets them manage any roadmap.</param>
public sealed record RoadmapActor(Guid EmployeeId, bool IsAdministrator);
