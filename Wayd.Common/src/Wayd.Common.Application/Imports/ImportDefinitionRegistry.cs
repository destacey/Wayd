using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Imports;

public interface IImportDefinitionRegistry
{
    Result<IImportDefinition> Find(string key);

    IReadOnlyList<IImportDefinition> All { get; }
}

/// <summary>
/// Resolves an import definition by the key stored on the run.
/// </summary>
/// <remarks>
/// Takes the definitions as an injected collection rather than resolving them from an
/// <c>IServiceProvider</c>: Wolverine's codegen constructor-inlines a handler's dependencies and service
/// location is disallowed, so a registry that held a provider would poison the generated tree.
/// </remarks>
public sealed class ImportDefinitionRegistry : IImportDefinitionRegistry
{
    private readonly Dictionary<string, IImportDefinition> _byKey;

    public ImportDefinitionRegistry(IEnumerable<IImportDefinition> definitions)
    {
        _byKey = definitions.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
        All = [.. _byKey.Values];

        foreach (var definition in All.Where(d => d.Atomicity == ImportAtomicity.PerGroup))
            EnsureGroupsCanHold(definition);
    }

    /// <summary>
    /// Refuses a per-group definition whose promise the runner cannot keep, at startup rather than on the
    /// first file someone imports.
    /// </summary>
    /// <remarks>
    /// A group is only whole within one pass: each pass saves before the next starts, so a group accepted by
    /// one pass and rejected by the next would be left half applied. An import that needs several passes
    /// will need them to share a transaction before it can apply per group.
    /// </remarks>
    private static void EnsureGroupsCanHold(IImportDefinition definition)
    {
        if (definition.Passes.Count != 1)
            throw new InvalidOperationException(
                $"'{definition.Key}' applies per group but has {definition.Passes.Count} passes; a group can only be kept whole within a single pass.");

        if (string.IsNullOrWhiteSpace(definition.GroupNoun))
            throw new InvalidOperationException(
                $"'{definition.Key}' applies per group but does not say what a group is.");
    }

    public IReadOnlyList<IImportDefinition> All { get; }

    /// <summary>
    /// Fails rather than throws for an unknown key: a run persisted under a definition that was later
    /// removed is data, not a programming error, and the Settings page has to render it.
    /// </summary>
    public Result<IImportDefinition> Find(string key) =>
        _byKey.TryGetValue(key, out var definition)
            ? Result.Success(definition)
            : Result.Failure<IImportDefinition>($"No import definition is registered for '{key}'.");
}
