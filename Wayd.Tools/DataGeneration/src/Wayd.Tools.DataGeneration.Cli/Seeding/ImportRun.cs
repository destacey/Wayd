namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// What one import run produced: the id each applied row created, keyed by the import id the row was
/// submitted under.
/// </summary>
/// <remarks>
/// This is the whole reason a seed has to wait for a run rather than firing files at the API. An import
/// references records by id, and the only place those ids exist is here — so a stage cannot write its file
/// until the stage before it has finished and reported them.
/// </remarks>
public sealed record ImportRun(Guid ProcessId, IReadOnlyDictionary<string, Guid> CreatedIdsByImportId)
{
    /// <summary>
    /// The id created for one submitted row, by the import id it was submitted under.
    /// </summary>
    /// <remarks>
    /// A miss is a bug in the seed rather than bad input: every row the generator writes is one it also
    /// resolves later, so failing loudly here beats writing a file full of empty references.
    /// </remarks>
    public Guid this[string importId] =>
        CreatedIdsByImportId.TryGetValue(importId, out var id)
            ? id
            : throw new SeedException(
                $"Import run {ProcessId} reported no record for row '{importId}'. "
                + "The generator referenced a row the import did not create.");

    public bool TryGet(string importId, out Guid id) => CreatedIdsByImportId.TryGetValue(importId, out id);
}
