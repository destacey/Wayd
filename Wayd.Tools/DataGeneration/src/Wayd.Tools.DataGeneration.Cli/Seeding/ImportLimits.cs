using Wayd.Tools.DataGeneration.Cli.Client;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// The most rows each import type accepts in one file, as the environment being seeded publishes them.
/// </summary>
/// <remarks>
/// The server owns these numbers and can change one per import type, so the seed reads them rather than
/// keeping a copy that would drift. Only imports the token may submit count: the definitions list also
/// carries every type a token with oversight of imports may view, and a cap for a file the seed would be
/// refused is no use to it.
/// </remarks>
public sealed class ImportLimits
{
    private readonly Dictionary<string, int> _maxRowsByKey;

    public ImportLimits(IEnumerable<ImportDefinitionDto> definitions)
    {
        _maxRowsByKey = definitions
            .Where(d => d.CanSubmit)
            .ToDictionary(d => d.Key, d => d.MaxRows, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The row cap the server publishes for one import type.</summary>
    public int MaxRows(string importKey) =>
        _maxRowsByKey.TryGetValue(importKey, out var maxRows)
            ? maxRows
            : throw new SeedException(Unsubmittable([importKey]));

    /// <summary>
    /// Fails when the token cannot submit any of the given import types, naming every one.
    /// </summary>
    public void Require(IEnumerable<string> importKeys)
    {
        var missing = importKeys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => !_maxRowsByKey.ContainsKey(k))
            .ToList();

        if (missing.Count > 0)
            throw new SeedException(Unsubmittable(missing));
    }

    private static string Unsubmittable(IReadOnlyList<string> importKeys) =>
        $"This token cannot submit {string.Join(", ", importKeys.Select(k => $"'{k}'"))}. "
        + "It may lack the permission for that import, or the API may be older than this tool.";
}
