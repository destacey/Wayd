using Wayd.Common.Domain.Employees;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// An employee invited to edit a story map. An editor can see the map when it is private and change its content
/// whatever its edit access; they still need the Story Maps View permission.
/// </summary>
public sealed class StoryMapEditor
{
    private StoryMapEditor() { }

    internal StoryMapEditor(Guid storyMapId, Guid employeeId)
    {
        StoryMapId = storyMapId;
        EmployeeId = employeeId;
    }

    /// <summary>The map the employee edits.</summary>
    public Guid StoryMapId { get; private init; }

    /// <summary>The editing employee.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>The editing employee, when loaded.</summary>
    public Employee? Employee { get; private init; }
}
