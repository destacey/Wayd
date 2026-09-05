using System.Text.Json;
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
public sealed class TestImportDefinition(ISerializerService serializer) : ImportDefinition<TestImportRow>(serializer)
{
    public override string Key => "test-import";
    public override string DisplayName => "Test Import";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Employees;
    public override int ChunkSize => 2;

    /// <summary>Rows each pass was handed, in order, so a test can assert on how the runner fed them.</summary>
    public List<(string Pass, string[] ImportIds, bool IsFinalChunk)> Calls { get; } = [];

    /// <summary>Set to make the pass itself fail, as distinct from a row failing.</summary>
    public string? PassFailure { get; set; }

    protected override IReadOnlyList<ImportPass<TestImportRow>> Steps =>
    [
        new("Create", ImportPassScope.Chunked, Create),
        new("Link", ImportPassScope.WholeSet, Link),
    ];

    private Task<Result> Create(ImportPassContext<TestImportRow> context, CancellationToken cancellationToken)
    {
        Calls.Add(("Create", [.. context.Rows.Select(r => r.ImportId)], context.IsFinalChunk));

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

/// <summary>The real System.Text.Json behaviour, without taking a dependency on Infrastructure.</summary>
public sealed class TestSerializerService : ISerializerService
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, _options);

    public string Serialize<T>(T obj, Type type) => JsonSerializer.Serialize(obj, type, _options);

    public T Deserialize<T>(string text) => JsonSerializer.Deserialize<T>(text, _options)!;
}
