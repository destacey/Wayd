using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Links;

namespace Wayd.Links;

/// <summary>
/// Answers what the caller may do with one record's links: the record's own rule, narrowed by a read-only token.
/// </summary>
/// <remarks>
/// The one path every link command and query takes, so the token limit is applied in one place and no owner type can
/// forget it. Registered by Infrastructure; public because Wolverine's generated handlers construct it.
/// </remarks>
public sealed class LinkOwnerAccessResolver(
    IEnumerable<ILinkOwnerType> ownerTypes,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser)
{
    private readonly IEnumerable<ILinkOwnerType> _ownerTypes = ownerTypes;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <summary>
    /// The caller, as link events record them.
    /// </summary>
    /// <remarks>
    /// The employee is the one the account is linked to now, the same answer the owner types decide access from, not
    /// the one a token carried when it was issued: an incident's timeline names the employee, and would otherwise
    /// record nothing for a caller linked since.
    /// </remarks>
    public async Task<EventActor> ResolveActor(CancellationToken cancellationToken) =>
        EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));

    /// <summary>
    /// What the caller may do with the record's links, under the registered spelling of its owner type. Fails when
    /// the owner type is not registered or no module answers for it; null when the caller cannot read the record or
    /// it does not exist.
    /// </summary>
    public async Task<Result<ResolvedLinkOwner?>> Resolve(string? ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var descriptor = LinkOwnerTypes.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<ResolvedLinkOwner?>(descriptor.Error);

        var implementation = _ownerTypes.FirstOrDefault(t =>
            string.Equals(t.OwnerType, descriptor.Value.Key, StringComparison.OrdinalIgnoreCase));
        if (implementation is null)
            return Result.Failure<ResolvedLinkOwner?>(LinkErrors.UnknownOwnerType(descriptor.Value.Key));

        var access = await implementation.Resolve(ownerId, cancellationToken);
        if (access is null)
            return Result.Success<ResolvedLinkOwner?>(null);

        var canManage = access.CanManage && !_currentPrincipal.IsReadOnly;

        return Result.Success<ResolvedLinkOwner?>(new ResolvedLinkOwner(descriptor.Value.Key, ownerId, canManage));
    }

    /// <summary>The record, when it exists, the caller can read it and may manage its links.</summary>
    public async Task<Result<ResolvedLinkOwner>> ForManaging(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var resolved = await Resolve(ownerType, ownerId, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<ResolvedLinkOwner>(resolved.Error);

        if (resolved.Value is not { } owner)
            return Result.Failure<ResolvedLinkOwner>(LinkAccessErrors.NotFound);

        return owner.CanManage
            ? owner
            : Result.Failure<ResolvedLinkOwner>(LinkAccessErrors.CannotManage);
    }
}

/// <summary>A record whose links the caller can read.</summary>
/// <param name="OwnerType">The registered spelling of the owner type, the one links are stored under.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="CanManage">Whether the caller may add, change and remove its links.</param>
public sealed record ResolvedLinkOwner(string OwnerType, Guid OwnerId, bool CanManage);
