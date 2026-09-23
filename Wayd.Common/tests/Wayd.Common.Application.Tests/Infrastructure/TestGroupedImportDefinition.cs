using CSharpFunctionalExtensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// Row shape for the definition below: the group it belongs to, and whether the pass rejects it. A row naming
/// another's import id in <paramref name="SameGroupAs"/> takes that row's group instead, which only the whole
/// file can answer.
/// </summary>
public sealed record TestGroupedImportRow(string Group, bool ShouldFail = false, string? SameGroupAs = null);

/// <summary>
/// A single-pass, per-group definition standing in for a real one, so the runner's grouping can be exercised
/// without a module's DbContext.
/// </summary>
public sealed class TestGroupedImportDefinition(IImportPayloadSerializer serializer) : ImportDefinition<TestGroupedImportRow>(serializer)
{
    public override string Key => "test-grouped-import";
    public override string DisplayName => "Test Grouped Import";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Employees;
    public override ImportAtomicity Atomicity => ImportAtomicity.PerGroup;
    public override string? GroupNoun => "widget";

    public int ChunkSizeOverride { get; init; } = 4;
    public override int ChunkSize => ChunkSizeOverride;

    /// <summary>Rows each call was handed, in order, so a test can assert on how the runner fed them.</summary>
    public List<string[]> Calls { get; } = [];

    protected override string? GroupKey(TestGroupedImportRow row) => row.Group;

    protected override IReadOnlyList<string?> GroupKeys(IReadOnlyList<(string ImportId, TestGroupedImportRow Row)> rows)
    {
        var groupsByImportId = rows.ToDictionary(r => r.ImportId, r => r.Row.Group);

        return [.. rows.Select(r => r.Row.SameGroupAs is { } other ? groupsByImportId[other] : r.Row.Group)];
    }

    protected override IReadOnlyList<ImportPass<TestGroupedImportRow>> Steps =>
    [
        new("Apply", ImportPassScope.Chunked, Apply),
    ];

    private Task<Result> Apply(ImportPassContext<TestGroupedImportRow> context, CancellationToken cancellationToken)
    {
        Calls.Add([.. context.Rows.Select(r => r.ImportId)]);

        foreach (var row in context.Rows)
        {
            if (row.Data.ShouldFail)
                row.Failed($"Row '{row.ImportId}' was marked to fail.");
            else
                row.Created(Guid.CreateVersion7());
        }

        return Task.FromResult(Result.Success());
    }
}
