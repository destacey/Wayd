using Wayd.Common.Domain.Employees;

namespace Wayd.Planning.Domain.Models.Roadmaps;

/// <summary>
/// An employee who owns a roadmap: its details, owners, visibility, state, colors and items.
/// </summary>
public sealed class RoadmapOwner
{
    private RoadmapOwner() { }

    internal RoadmapOwner(Roadmap roadmap, Guid employeeId)
    {
        RoadmapId = roadmap.Id;
        EmployeeId = employeeId;
    }

    /// <summary>The roadmap this employee owns.</summary>
    public Guid RoadmapId { get; private init; }

    /// <summary>The owning employee.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>The owning employee, when loaded.</summary>
    public Employee? Employee { get; private init; }
}
