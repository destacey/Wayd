using CSharpFunctionalExtensions;

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
