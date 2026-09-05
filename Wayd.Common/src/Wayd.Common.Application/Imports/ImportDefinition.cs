using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports;

/// <summary>
/// Base for an import definition, typed to its row DTO.
/// </summary>
/// <remarks>
/// Owns the payload round-trip so no definition writes its own deserialization, and so every import agrees
/// on how a row is stored — the payload is written once at submission and read back by a worker that may be
/// running days later, after a resume.
/// </remarks>
public abstract class ImportDefinition<TRow>(ISerializerService serializer) : IImportDefinition
{
    private readonly ISerializerService _serializer = serializer;

    public abstract string Key { get; }
    public abstract string DisplayName { get; }
    public abstract string PermissionAction { get; }
    public abstract string PermissionResource { get; }

    public virtual ImportAtomicity Atomicity => ImportAtomicity.PerRow;
    public virtual int InlineThreshold => 100;
    public virtual int MaxRows => 50_000;
    public virtual int ChunkSize => 500;

    /// <summary>The ordered steps this import runs. Declared once; the runner drives them.</summary>
    protected abstract IReadOnlyList<ImportPass<TRow>> Steps { get; }

    public IReadOnlyList<ImportPassDescriptor> Passes =>
        [.. Steps.Select(s => new ImportPassDescriptor(s.Name, s.Scope))];

    public async Task<Result<ImportPassResult>> ExecutePass(
        Guid importProcessId,
        int passIndex,
        IReadOnlyList<ImportProcessRow> rows,
        bool isFinalChunk,
        CancellationToken cancellationToken)
    {
        var steps = Steps;
        if (passIndex < 0 || passIndex >= steps.Count)
            return Result.Failure<ImportPassResult>($"'{Key}' has no pass at index {passIndex}.");

        var items = new List<ImportRowItem<TRow>>(rows.Count);
        foreach (var row in rows)
        {
            // A row whose payload the retention sweep emptied cannot be executed. Reject it here rather
            // than letting a deserialization of null surface as a confusing exception.
            if (row.Payload is null)
                return Result.Failure<ImportPassResult>(
                    $"Row '{row.ImportId}' has no data left to apply; it has passed its retention window.");

            items.Add(new ImportRowItem<TRow>(row.ImportId, row.RowNumber, _serializer.Deserialize<TRow>(row.Payload)));
        }

        var context = new ImportPassContext<TRow>(importProcessId, steps[passIndex].Name, items, isFinalChunk);

        var result = await steps[passIndex].Execute(context, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<ImportPassResult>(result.Error);

        return Result.Success(new ImportPassResult(
            [.. items.Select(i => new ImportRowResult(i.ImportId, i.IsFailed, i.Error, i.CreatedEntityIdSet ? i.CreatedEntityId : null, i.Warning))]));
    }

    /// <summary>Serializes a parsed row for storage at submission time.</summary>
    public string SerializeRow(TRow row) => _serializer.Serialize(row);

    public string SerializeRow(object row) => row is TRow typed
        ? SerializeRow(typed)
        : throw new ArgumentException($"'{Key}' imports {typeof(TRow).Name}, not {row.GetType().Name}.", nameof(row));
}
