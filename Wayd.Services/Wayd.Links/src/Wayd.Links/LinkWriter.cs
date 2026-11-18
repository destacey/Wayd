using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Links;

namespace Wayd.Links;

/// <summary>
/// The one implementation of <see cref="ILinkWriter"/>. Registered by Infrastructure; public because Wolverine's
/// generated handlers construct it.
/// </summary>
public sealed class LinkWriter(
    ILinksDbContext dbContext,
    IEnumerable<ILinkOwnerType> ownerTypes,
    IDateTimeProvider dateTimeProvider) : ILinkWriter
{
    private readonly ILinksDbContext _dbContext = dbContext;
    private readonly IEnumerable<ILinkOwnerType> _ownerTypes = ownerTypes;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc/>
    public async Task<Result<Guid>> Add(string ownerType, Guid ownerId, string name, string url, EventActor actor, CancellationToken cancellationToken)
    {
        var descriptor = LinkOwnerTypes.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<Guid>(descriptor.Error);

        var key = descriptor.Value.Key;
        var now = _dateTimeProvider.Now;
        var created = Link.Create(key, ownerId, name, url, actor, now);
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error);

        var link = created.Value;
        _dbContext.Links.Add(link);

        await TellOwner(new LinkChange(LinkChangeKind.Added, link.Id, ownerId, link.Details, null, actor, now), key, cancellationToken);

        return link.Id;
    }

    /// <inheritdoc/>
    public async Task<Result> Update(string ownerType, Guid ownerId, Guid linkId, string name, string url, EventActor actor, CancellationToken cancellationToken)
    {
        var link = await Find(ownerType, ownerId, linkId, cancellationToken);
        if (link.IsFailure)
            return link;

        var now = _dateTimeProvider.Now;
        var previous = link.Value.Details;

        var updated = link.Value.UpdateDetails(name, url, actor, now);
        if (updated.IsFailure)
            return updated;

        if (link.Value.Details != previous)
        {
            await TellOwner(
                new LinkChange(LinkChangeKind.Updated, linkId, ownerId, link.Value.Details, previous, actor, now),
                link.Value.OwnerType,
                cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> Remove(string ownerType, Guid ownerId, Guid linkId, EventActor actor, CancellationToken cancellationToken)
    {
        var link = await Find(ownerType, ownerId, linkId, cancellationToken);
        if (link.IsFailure)
            return link;

        await Remove(link.Value, actor, tellOwner: true, cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task RemoveAll(string ownerType, Guid ownerId, EventActor actor, CancellationToken cancellationToken)
    {
        var descriptor = LinkOwnerTypes.Resolve(ownerType);
        if (descriptor.IsFailure)
            throw new ArgumentException(descriptor.Error, nameof(ownerType));

        var key = descriptor.Value.Key;
        var links = await _dbContext.Links
            .Where(l => l.OwnerType == key && l.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        // The owner is not told: the record these links were on is the thing being deleted.
        foreach (var link in links)
            await Remove(link, actor, tellOwner: false, cancellationToken);
    }

    private async Task<Result<Link>> Find(string ownerType, Guid ownerId, Guid linkId, CancellationToken cancellationToken)
    {
        var descriptor = LinkOwnerTypes.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<Link>(descriptor.Error);

        var key = descriptor.Value.Key;
        var link = await _dbContext.Links
            .FirstOrDefaultAsync(l => l.Id == linkId && l.OwnerType == key && l.OwnerId == ownerId, cancellationToken);

        return link is null ? Result.Failure<Link>(LinkAccessErrors.NotFound) : link;
    }

    private async Task Remove(Link link, EventActor actor, bool tellOwner, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;

        link.Remove(actor, now);
        _dbContext.Links.Remove(link);

        if (tellOwner)
        {
            await TellOwner(
                new LinkChange(LinkChangeKind.Removed, link.Id, link.OwnerId, link.Details, null, actor, now),
                link.OwnerType,
                cancellationToken);
        }
    }

    private Task TellOwner(LinkChange change, string ownerType, CancellationToken cancellationToken)
    {
        var implementation = _ownerTypes.FirstOrDefault(t =>
            string.Equals(t.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));

        return implementation is null ? Task.CompletedTask : implementation.OnChanged(change, cancellationToken);
    }
}
