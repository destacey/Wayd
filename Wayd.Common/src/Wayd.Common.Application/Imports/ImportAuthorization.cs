using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;

namespace Wayd.Common.Application.Imports;

/// <summary>
/// Resolves the definition governing a run and checks the caller against it.
/// </summary>
/// <remarks>
/// There is no single "imports" permission: what gates a run is whatever gates submitting that kind of
/// file, which the definition declares. So every one of these endpoints authorizes on a value read from
/// the run rather than on an attribute the framework could enforce ahead of the handler.
/// </remarks>
internal static class ImportAuthorization
{
    public static async Task<Result<IImportDefinition>> ResolveFor(
        IImportDefinitionRegistry registry,
        ICurrentPrincipal currentPrincipal,
        string importType,
        CancellationToken cancellationToken)
    {
        var definition = registry.Find(importType);
        if (definition.IsFailure)
            return Result.Failure<IImportDefinition>(definition.Error);

        var permission = ApplicationPermission.NameFor(
            definition.Value.PermissionAction, definition.Value.PermissionResource);

        if (!await currentPrincipal.HasPermission(permission, cancellationToken))
            return Result.Failure<IImportDefinition>($"You do not have permission to work with {definition.Value.DisplayName} imports.");

        return Result.Success(definition.Value);
    }
}
