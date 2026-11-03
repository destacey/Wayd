using Wayd.Common.Domain.Employees;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// An employee who owns a story map: its lifecycle, details, owners, editors, visibility and who can edit.
/// </summary>
public sealed class StoryMapOwner
{
    private StoryMapOwner() { }

    internal StoryMapOwner(Guid storyMapId, Guid employeeId)
    {
        StoryMapId = storyMapId;
        EmployeeId = employeeId;
    }

    /// <summary>The map this employee owns.</summary>
    public Guid StoryMapId { get; private init; }

    /// <summary>The owning employee.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>The owning employee, when loaded.</summary>
    public Employee? Employee { get; private init; }
}
