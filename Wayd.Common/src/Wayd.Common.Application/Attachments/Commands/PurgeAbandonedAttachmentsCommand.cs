using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Attachments;

namespace Wayd.Common.Application.Attachments.Commands;

/// <summary>
/// Deletes every upload that no save claimed within <see cref="AttachmentLimits.PendingRetention"/> — pasted into a
/// form that was then cancelled, or removed before saving — answering how many were deleted.
/// </summary>
/// <remarks>
/// Claimed attachments are not its concern: the save that stops referring to one deletes it, and deleting a record
/// deletes its attachments.
/// </remarks>
public sealed record PurgeAbandonedAttachmentsCommand : ICommand<int>, ILongRunningRequest;

/// <summary>Deletes abandoned uploads in batches, each batch its own save.</summary>
public sealed class PurgeAbandonedAttachmentsCommandHandler(
    IAttachmentsDbContext dbContext,
    IAttachmentStore store,
    IDateTimeProvider dateTimeProvider,
    ILogger<PurgeAbandonedAttachmentsCommandHandler> logger)
    : ICommandHandler<PurgeAbandonedAttachmentsCommand, int>
{
    /// <summary>The most attachments one save deletes.</summary>
    public const int BatchSize = 100;

    private readonly IAttachmentsDbContext _dbContext = dbContext;
    private readonly IAttachmentStore _store = store;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<PurgeAbandonedAttachmentsCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(PurgeAbandonedAttachmentsCommand request, CancellationToken cancellationToken)
    {
        // Fixed once, so uploads made while the sweep runs are never in reach of it.
        var cutoff = _dateTimeProvider.Now - AttachmentLimits.PendingRetention;

        var purged = 0;
        List<Attachment> batch;
        do
        {
            batch = await _dbContext.Attachments
                .Where(a => a.OwnerId == null && a.UploadedOn < cutoff)
                .OrderBy(a => a.UploadedOn)
                .ThenBy(a => a.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var attachment in batch)
            {
                _store.Delete(attachment.Id);
                _dbContext.Attachments.Remove(attachment);
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A save claimed one of the batch after it was read: its owner is the concurrency token, so the
                // delete matched nothing and the whole batch rolled back. The next run reads them afresh.
                _logger.LogInformation("Attachment sweep stopped: an upload in its batch was claimed while it ran.");
                break;
            }

            purged += batch.Count;
        }
        while (batch.Count == BatchSize);

        if (purged > 0)
            _logger.LogInformation("Attachment sweep deleted {Count} upload(s) never claimed within {Retention}.", purged, AttachmentLimits.PendingRetention);

        return Result.Success(purged);
    }
}
