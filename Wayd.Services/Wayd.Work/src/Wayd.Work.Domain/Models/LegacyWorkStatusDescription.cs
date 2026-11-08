namespace Wayd.Work.Domain.Models;

/// <summary>
/// A description an administrator gave one of Work's shared statuses before statuses moved into the status
/// workflow engine, kept by the status's name so the sync can give it to each engine status of that name.
/// </summary>
/// <remarks>
/// Written once by the migration that removed Work's shared statuses, and only read after that. The engine held
/// no work item statuses on an upgrade from before the move, so the descriptions could not be copied onto them
/// when the migration ran; the sync copies them as it creates the statuses.
/// </remarks>
public sealed class LegacyWorkStatusDescription
{
    private LegacyWorkStatusDescription() { }

    /// <summary>The shared status's name, matched against an engine status's name ignoring case.</summary>
    public string Name { get; private init; } = null!;

    /// <summary>The description the shared status had.</summary>
    public string Description { get; private init; } = null!;
}
