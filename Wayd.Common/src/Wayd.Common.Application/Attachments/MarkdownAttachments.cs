using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Attachments;

namespace Wayd.Common.Application.Attachments;

/// <inheritdoc cref="IMarkdownAttachments"/>
/// <remarks>Public because Wolverine's generated handlers construct it directly.</remarks>
public sealed class MarkdownAttachments(
    IAttachmentsDbContext dbContext,
    IAttachmentStore store,
    AttachmentAccess access,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IMarkdownAttachments
{
    private readonly IAttachmentsDbContext _dbContext = dbContext;
    private readonly IAttachmentStore _store = store;
    private readonly AttachmentAccess _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    // Copies made in this unit of work, which the database does not hold until the caller saves.
    private readonly List<Attachment> _copies = [];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string?>> CopyForeign(string ownerType, Guid? ownerId, IReadOnlyList<string?> markdown, CancellationToken cancellationToken)
    {
        var key = _access.OwnerTypeKey(ownerType);
        var referenced = await Referenced(markdown, cancellationToken);
        if (referenced.Count == 0)
        {
            return markdown;
        }

        var userId = _currentUser.GetUserId();
        var now = _dateTimeProvider.Now;
        var replacements = new Dictionary<Guid, Guid>();
        foreach (var attachment in referenced)
        {
            if (IsOwnPending(attachment, userId) || (ownerId is { } id && attachment.IsOwnedBy(key, id)))
            {
                continue;
            }

            if (!await _access.CanRead(attachment, cancellationToken))
            {
                continue;
            }

            var copy = attachment.CopyFor(userId, now);
            _dbContext.Attachments.Add(copy);
            _copies.Add(copy);
            await _store.Copy(attachment.Id, copy.Id, cancellationToken);
            replacements[attachment.Id] = copy.Id;
        }

        return replacements.Count == 0
            ? markdown
            : [.. markdown.Select(text => AttachmentReferences.Replace(text, replacements))];
    }

    /// <inheritdoc/>
    public async Task Claim(string ownerType, Guid ownerId, IReadOnlyList<string?> markdown, CancellationToken cancellationToken)
    {
        var key = _access.OwnerTypeKey(ownerType);
        var userId = _currentUser.GetUserId();
        var now = _dateTimeProvider.Now;

        var referenced = await Referenced(markdown, cancellationToken);
        foreach (var attachment in referenced.Where(a => IsOwnPending(a, userId)))
        {
            attachment.ClaimFor(key, ownerId, now);
        }

        var referencedIds = referenced.Select(a => a.Id).ToHashSet();
        var owned = await _dbContext.Attachments
            .Where(a => a.OwnerType == key && a.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        foreach (var attachment in owned.Where(a => !referencedIds.Contains(a.Id)))
        {
            Remove(attachment);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAll(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var key = _access.OwnerTypeKey(ownerType);
        var owned = await _dbContext.Attachments
            .Where(a => a.OwnerType == key && a.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        foreach (var attachment in owned)
        {
            Remove(attachment);
        }
    }

    private static bool IsOwnPending(Attachment attachment, string userId) =>
        attachment.IsPending && string.Equals(attachment.UploadedBy, userId, StringComparison.Ordinal);

    /// <summary>
    /// The attachments the texts refer to, tracked, including copies made earlier in this unit of work that the
    /// database does not hold yet.
    /// </summary>
    private async Task<List<Attachment>> Referenced(IReadOnlyList<string?> markdown, CancellationToken cancellationToken)
    {
        var ids = AttachmentReferences.In(markdown);
        if (ids.Count == 0)
        {
            return [];
        }

        var stored = await _dbContext.Attachments
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(cancellationToken);
        var storedIds = stored.Select(a => a.Id).ToHashSet();

        return [.. stored, .. _copies.Where(c => ids.Contains(c.Id) && !storedIds.Contains(c.Id))];
    }

    private void Remove(Attachment attachment)
    {
        _store.Delete(attachment.Id);
        _dbContext.Attachments.Remove(attachment);
    }
}
