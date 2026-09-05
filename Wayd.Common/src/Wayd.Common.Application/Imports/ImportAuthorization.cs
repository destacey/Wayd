using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;

namespace Wayd.Common.Application.Imports;

/// <summary>
/// Who may see, and who may act on, an import run.
/// </summary>
/// <remarks>
/// Two tiers, because they answer different questions.
/// <para>
/// Being allowed to submit a kind of file is what entitles you to see how it went — the definition names
/// that permission, so it is only knowable once the run has been read and can never be an attribute. This
/// tier alone is what keeps anyone from being locked out of the result of an import they just ran.
/// </para>
/// <para>
/// <see cref="ApplicationResource.Imports"/> <c>View</c> is the second tier: oversight of every import
/// type for someone who watches the queue without submitting files. It is read-only on purpose — acting
/// on a run means changing the records that run created, which is what the first tier gates.
/// </para>
/// </remarks>
internal static class ImportAuthorization
{
    private static readonly string _viewAllPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Imports);

    /// <summary>Whether the caller oversees every import type, rather than only the ones they submit.</summary>
    public static Task<bool> CanViewAll(ICurrentPrincipal currentPrincipal, CancellationToken cancellationToken) =>
        currentPrincipal.HasPermission(_viewAllPermission, cancellationToken);

    /// <summary>
    /// The definitions whose runs the caller may read. A listing filters on these rather than checking
    /// each run it finds, so a page of any size costs one permission lookup per import type.
    /// </summary>
    /// <remarks>
    /// A run whose type no longer has a definition matches nothing here and so never appears — there is no
    /// permission left to check it against, and guessing one would be the wrong way to be helpful.
    /// </remarks>
    public static async Task<List<IImportDefinition>> Viewable(
        IImportDefinitionRegistry registry,
        ICurrentPrincipal currentPrincipal,
        CancellationToken cancellationToken) =>
        await CanViewAll(currentPrincipal, cancellationToken)
            ? [.. registry.All]
            : await Submittable(registry, currentPrincipal, cancellationToken);

    /// <summary>The definitions the caller may submit — and therefore may act on the runs of.</summary>
    public static async Task<List<IImportDefinition>> Submittable(
        IImportDefinitionRegistry registry,
        ICurrentPrincipal currentPrincipal,
        CancellationToken cancellationToken)
    {
        List<IImportDefinition> permitted = [];

        // Sequentially, not in parallel: the permission lookup reads through the DbContext, which is not
        // thread-safe.
        foreach (var definition in registry.All)
        {
            if (await CanSubmit(definition, currentPrincipal, cancellationToken))
                permitted.Add(definition);
        }

        return permitted;
    }

    public static Task<bool> CanSubmit(
        IImportDefinition definition,
        ICurrentPrincipal currentPrincipal,
        CancellationToken cancellationToken) =>
        currentPrincipal.HasPermission(
            ApplicationPermission.NameFor(definition.PermissionAction, definition.PermissionResource),
            cancellationToken);

    /// <summary>Resolves the definition governing a run for reading, which either tier allows.</summary>
    public static async Task<Result<IImportDefinition>> ResolveForRead(
        IImportDefinitionRegistry registry,
        ICurrentPrincipal currentPrincipal,
        string importType,
        CancellationToken cancellationToken)
    {
        var definition = registry.Find(importType);
        if (definition.IsFailure)
            return Result.Failure<IImportDefinition>(definition.Error);

        if (await CanViewAll(currentPrincipal, cancellationToken))
            return definition;

        return await CanSubmit(definition.Value, currentPrincipal, cancellationToken)
            ? definition
            : Denied(definition.Value);
    }

    /// <summary>
    /// Resolves the definition governing a run for a change to it. Only the submit permission will do:
    /// stopping, resuming or retrying a run alters the records it creates, which oversight does not cover.
    /// </summary>
    public static async Task<Result<IImportDefinition>> ResolveForManage(
        IImportDefinitionRegistry registry,
        ICurrentPrincipal currentPrincipal,
        string importType,
        CancellationToken cancellationToken)
    {
        var definition = registry.Find(importType);
        if (definition.IsFailure)
            return Result.Failure<IImportDefinition>(definition.Error);

        return await CanSubmit(definition.Value, currentPrincipal, cancellationToken)
            ? definition
            : Denied(definition.Value);
    }

    private static Result<IImportDefinition> Denied(IImportDefinition definition) =>
        Result.Failure<IImportDefinition>(
            $"You do not have permission to work with {definition.DisplayName} imports.");
}
