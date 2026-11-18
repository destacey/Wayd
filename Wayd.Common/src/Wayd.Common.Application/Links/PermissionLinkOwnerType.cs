namespace Wayd.Common.Application.Links;

/// <summary>
/// The owner type of a record whose own edit is decided by a permission alone: reading its links needs the
/// permission that reads the record, and changing them the permission that edits it.
/// </summary>
/// <remarks>
/// A record with a rule beyond the permission — a role on the record, a membership, a state that makes it read-only —
/// implements <see cref="ILinkOwnerType"/> itself.
/// </remarks>
public abstract class PermissionLinkOwnerType(ICurrentPrincipal currentPrincipal) : ILinkOwnerType
{
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <inheritdoc/>
    public abstract string OwnerType { get; }

    /// <summary>The permission the record's read endpoints require.</summary>
    protected abstract string ViewPermission { get; }

    /// <summary>The permission the record's own edit requires.</summary>
    protected abstract string ManagePermission { get; }

    /// <summary>Whether the record exists.</summary>
    protected abstract Task<bool> Exists(Guid ownerId, CancellationToken cancellationToken);

    /// <inheritdoc/>
    public async Task<LinkOwnerAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ViewPermission, cancellationToken))
            return null;

        if (!await Exists(ownerId, cancellationToken))
            return null;

        return new LinkOwnerAccess(await _currentPrincipal.HasPermission(ManagePermission, cancellationToken));
    }
}
