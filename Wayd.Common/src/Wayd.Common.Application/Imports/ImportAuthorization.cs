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

    /// <summary>
    /// The definitions this caller may work with. A listing filters on these rather than checking each run
    /// it finds, so one permission lookup per import type answers a page of any size.
    /// </summary>
    /// <remarks>
    /// A run whose type no longer has a definition matches nothing here and so never appears. There is no
    /// permission left to check it against, and guessing one would be the wrong way to be helpful.
    /// </remarks>
    public static async Task<List<IImportDefinition>> Permitted(
        IImportDefinitionRegistry registry,
        ICurrentPrincipal currentPrincipal,
        CancellationToken cancellationToken)
    {
        List<IImportDefinition> permitted = [];

        // Sequentially, not in parallel: the permission lookup reads through the DbContext, which is not
        // thread-safe.
        foreach (var definition in registry.All)
        {
            var permission = ApplicationPermission.NameFor(definition.PermissionAction, definition.PermissionResource);

            if (await currentPrincipal.HasPermission(permission, cancellationToken))
                permitted.Add(definition);
        }

        return permitted;
    }
}
