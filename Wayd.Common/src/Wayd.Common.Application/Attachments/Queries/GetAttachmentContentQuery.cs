using Wayd.Common.Application.Attachments.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Attachments.Queries;

/// <summary>
/// An attachment's bytes, or null when it does not exist or the caller may not read it — which every caller reports
/// as not found, so a response never confirms that an image exists.
/// </summary>
public sealed record GetAttachmentContentQuery(Guid Id) : IQuery<AttachmentContentDto?>;

/// <summary>Asks who may read the attachment before loading its bytes.</summary>
public sealed class GetAttachmentContentQueryHandler(
    IAttachmentsDbContext dbContext,
    IAttachmentStore store,
    AttachmentAccess access)
    : IQueryHandler<GetAttachmentContentQuery, AttachmentContentDto?>
{
    private readonly IAttachmentsDbContext _dbContext = dbContext;
    private readonly IAttachmentStore _store = store;
    private readonly AttachmentAccess _access = access;

    public async Task<AttachmentContentDto?> Handle(GetAttachmentContentQuery request, CancellationToken cancellationToken)
    {
        var attachment = await _dbContext.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (attachment is null || !await _access.CanRead(attachment, cancellationToken))
        {
            return null;
        }

        var data = await _store.Read(attachment.Id, cancellationToken);

        return data is null ? null : new AttachmentContentDto(data, attachment.ContentType, attachment.Sha256);
    }
}
