using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Interfaces;

namespace Wayd.Common.Application.Imports.Queries;

/// <summary>
/// The import types this caller may see — the type filter on the Imports page. An upload form wants the
/// ones they may submit, which oversight of every type does not grant; that is what <c>CanSubmit</c> says.
/// </summary>
public sealed record GetImportDefinitionsQuery : IQuery<IReadOnlyList<ImportDefinitionDto>>;

public sealed class GetImportDefinitionsQueryHandler(
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal) : IQueryHandler<GetImportDefinitionsQuery, IReadOnlyList<ImportDefinitionDto>>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IReadOnlyList<ImportDefinitionDto>> Handle(
        GetImportDefinitionsQuery query, CancellationToken cancellationToken)
    {
        var viewable = await ImportAuthorization.Viewable(_registry, _currentPrincipal, cancellationToken);
        var submittable = await ImportAuthorization.Submittable(_registry, _currentPrincipal, cancellationToken);
        var submittableKeys = submittable.Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<ImportDefinitionDto> definitions = [];

        // Sequentially, not in parallel: the flag lookup reads through the DbContext, which is not thread-safe.
        foreach (var d in viewable.OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            definitions.Add(new ImportDefinitionDto(
                d.Key, d.DisplayName, d.Atomicity, d.GroupNoun, d.MaxRows, d.PreflightMaxRows,
                submittableKeys.Contains(d.Key), await d.IsEnabled(cancellationToken)));
        }

        return definitions;
    }
}
