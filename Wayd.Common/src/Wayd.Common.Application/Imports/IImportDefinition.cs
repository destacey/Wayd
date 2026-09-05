using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports;

/// <summary>
/// Everything the runner, the endpoint and the Settings page need to know about one kind of import.
/// </summary>
/// <remarks>
/// Declared per import type rather than inferred, because the values genuinely differ: a strategic-theme
/// row is trivial while a project row resolves six references, so one global chunk size or inline threshold
/// would be wrong for most of them.
/// </remarks>
public interface IImportDefinition
{
    /// <summary>Stable key stored on <see cref="ImportProcess.ImportType"/>. Do not rename once shipped.</summary>
    string Key { get; }

    /// <summary>Shown wherever a person sees the import listed.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether a bad row keeps the rest of the file out. Per import type, not global: the team-hierarchy
    /// import is all-or-nothing on purpose, because half an imported hierarchy is worse than none.
    /// </summary>
    ImportAtomicity Atomicity { get; }

    /// <summary>At or below this many rows the file is applied in the request instead of queued.</summary>
    int InlineThreshold { get; }

    /// <summary>Above this many rows the submission is rejected outright, before anything is persisted.</summary>
    int MaxRows { get; }

    /// <summary>How many rows a chunked pass takes at a time.</summary>
    int ChunkSize { get; }

    /// <summary>
    /// The permission that gates submitting this import — and therefore seeing its history. The mapping is
    /// not uniform (the team-members import is gated on managing memberships rather than on an Import
    /// action), so it is declared here rather than reflected off a controller attribute.
    /// </summary>
    string PermissionAction { get; }
    string PermissionResource { get; }

    /// <summary>
    /// Runs one pass over one set of rows and reports what happened to each. Non-generic so the runner can
    /// drive any definition; the row type is the definition's own business.
    /// </summary>
    /// <remarks>
    /// A failed <see cref="Result"/> means the pass itself could not run — the whole attempt stops. Rows
    /// rejected individually come back in <see cref="ImportPassResult"/> and do not stop the run.
    /// </remarks>
    Task<Result<ImportPassResult>> ExecutePass(
        Guid importProcessId,
        int passIndex,
        IReadOnlyList<ImportProcessRow> rows,
        bool isFinalChunk,
        CancellationToken cancellationToken);

    /// <summary>The ordered passes. Index into this is what <c>ExecutePass</c> takes.</summary>
    IReadOnlyList<ImportPassDescriptor> Passes { get; }

    /// <summary>
    /// Serializes a parsed row for storage at submission. Non-generic so an endpoint can store rows without
    /// knowing the definition's row type; the runtime type must be the one the definition declares.
    /// </summary>
    string SerializeRow(object row);
}

/// <summary>The runner's view of a pass: what it is called and whether it may be chunked.</summary>
public sealed record ImportPassDescriptor(string Name, ImportPassScope Scope);

/// <summary>
/// What one pass did to the rows it was given.
/// </summary>
/// <remarks>
/// The runner, not the pass, decides row state: a rejected row is marked failed at once so later passes
/// skip it, but a row is only marked succeeded after the <em>final</em> pass — an employee created by pass
/// one is not finished until the manager and deactivation passes have had their turn.
/// </remarks>
public sealed record ImportPassResult(IReadOnlyList<ImportRowResult> Rows);

public sealed record ImportRowResult(string ImportId, bool Failed, string? Error, Guid? CreatedEntityId, string? Warning);
