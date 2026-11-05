using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Domain.Tests.Data;

/// <summary>
/// The people story map tests act as. Generated and seeded maps are owned by <see cref="OwnerEmployeeId"/>.
/// </summary>
public static class StoryMapActors
{
    public static readonly Guid OwnerEmployeeId = Guid.Parse("5e0a0c00-0000-4000-a000-00000000f001");

    /// <summary>The owner of a generated or seeded map.</summary>
    public static readonly StoryMapActor Owner = new(OwnerEmployeeId, false, new HashSet<Guid>());

    /// <summary>A Story Maps administrator who is not on any map.</summary>
    public static readonly StoryMapActor Administrator = new(Guid.Parse("5e0a0c00-0000-4000-a000-00000000a001"), true, new HashSet<Guid>());

    /// <summary>Someone who can see Story Maps but is on no map.</summary>
    public static readonly StoryMapActor Viewer = new(Guid.Parse("5e0a0c00-0000-4000-a000-00000000b001"), false, new HashSet<Guid>());

    /// <summary>Someone who is the given employee and belongs to <paramref name="teamIds"/>.</summary>
    public static StoryMapActor Employee(Guid employeeId, params Guid[] teamIds) =>
        new(employeeId, false, teamIds.ToHashSet());
}
