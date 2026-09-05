using CSharpFunctionalExtensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>Row shape for the definition below.</summary>
public sealed record TestImportRow(string Name, bool ShouldFail = false);

/// <summary>
/// A two-pass definition standing in for a real one, so the base class and the registry can be exercised
/// without dragging a module's DbContext into these tests.
/// </summary>
public sealed class TestImportDefinition(IImportPayloadSerializer serializer) : ImportDefinition<TestImportRow>(serializer)
{
    /// <summary>
    /// Overridable so a test can stand up a second definition and tell the two apart — a listing filtered
    /// by permission needs one type the caller may see and one it may not.
    /// </summary>
    public string KeyOverride { get; init; } = "test-import";
    public string DisplayNameOverride { get; init; } = "Test Import";
    public string PermissionResourceOverride { get; init; } = ApplicationResource.Employees;

    public override string Key => KeyOverride;
    public override string DisplayName => DisplayNameOverride;
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => PermissionResourceOverride;
    public override int ChunkSize => 2;

    /// <summary>Rows each pass was handed, in order, so a test can assert on how the runner fed them.</summary>
    public List<(string Pass, string[] ImportIds, bool IsFinalChunk)> Calls { get; } = [];

    /// <summary>Set to make the pass itself fail, as distinct from a row failing.</summary>
    public string? PassFailure { get; set; }

    /// <summary>Runs before each pass call, so a test can simulate something arriving mid-run.</summary>
    public Action? BeforePass { get; set; }

    /// <summary>Atomicity is overridable so one definition can stand in for both shapes.</summary>
    public ImportAtomicity AtomicityOverride { get; set; } = ImportAtomicity.PerRow;

    public override ImportAtomicity Atomicity => AtomicityOverride;

    protected override IReadOnlyList<ImportPass<TestImportRow>> Steps =>
    [
        new("Create", ImportPassScope.Chunked, Create),
        new("Link", ImportPassScope.WholeSet, Link),
    ];

    private Task<Result> Create(ImportPassContext<TestImportRow> context, CancellationToken cancellationToken)
    {
        Calls.Add(("Create", [.. context.Rows.Select(r => r.ImportId)], context.IsFinalChunk));
        BeforePass?.Invoke();

        if (PassFailure is not null)
            return Task.FromResult(Result.Failure(PassFailure));

        foreach (var row in context.Rows)
        {
            if (row.Data.ShouldFail)
                row.Failed($"Row '{row.ImportId}' was marked to fail.");
            else
                row.Created(Guid.CreateVersion7());
        }

        return Task.FromResult(Result.Success());
    }

    private Task<Result> Link(ImportPassContext<TestImportRow> context, CancellationToken cancellationToken)
    {
        Calls.Add(("Link", [.. context.Rows.Select(r => r.ImportId)], context.IsFinalChunk));
        return Task.FromResult(Result.Success());
    }
}
