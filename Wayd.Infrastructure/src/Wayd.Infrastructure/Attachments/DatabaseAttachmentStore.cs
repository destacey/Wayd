using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Domain.Attachments;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.Attachments;

/// <summary>
/// Keeps attachment bytes in <c>App.AttachmentContents</c>, through the request's own context, so they are
/// written and deleted by the same save as the attachment rows.
/// </summary>
/// <remarks>Public because Wolverine's generated handlers construct it directly.</remarks>
public sealed class DatabaseAttachmentStore(WaydDbContext dbContext) : IAttachmentStore
{
    private readonly WaydDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public void Write(Guid id, byte[] data) => _dbContext.AttachmentContents.Add(new AttachmentContent(id, data));

    /// <inheritdoc/>
    public async Task Copy(Guid sourceId, Guid targetId, CancellationToken cancellationToken)
    {
        var data = await Read(sourceId, cancellationToken)
            ?? throw new InvalidOperationException($"Attachment {sourceId} has no stored bytes to copy.");

        Write(targetId, data);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> Read(Guid id, CancellationToken cancellationToken)
    {
        var added = _dbContext.AttachmentContents.Local.FirstOrDefault(c => c.AttachmentId == id);
        if (added is not null)
        {
            return added.Data;
        }

        return await _dbContext.AttachmentContents
            .Where(c => c.AttachmentId == id)
            .Select(c => c.Data)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public void Delete(Guid id)
    {
        var tracked = _dbContext.AttachmentContents.Local.FirstOrDefault(c => c.AttachmentId == id);
        if (tracked is not null)
        {
            _dbContext.AttachmentContents.Remove(tracked);
            return;
        }

        // A stand-in with only the key: deleting by key needs no bytes loaded.
        _dbContext.AttachmentContents.Remove(new AttachmentContent(id, []));
    }
}
