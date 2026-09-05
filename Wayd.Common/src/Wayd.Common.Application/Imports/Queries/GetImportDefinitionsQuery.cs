using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Interfaces;

namespace Wayd.Common.Application.Imports.Queries;

/// <summary>
/// The import types this caller may submit — the type filter on the Imports page, and what an upload form
/// would offer.
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
        var permitted = await ImportAuthorization.Permitted(_registry, _currentPrincipal, cancellationToken);

        return
        [
            .. permitted
                .OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(d => new ImportDefinitionDto(d.Key, d.DisplayName, d.Atomicity, d.MaxRows, d.InlineThreshold))
        ];
    }
}
