using Wayd.Common.Domain.Attachments;

namespace Wayd.Common.Application.Persistence;

/// <summary>
/// The table that says who each image placed in markdown belongs to. Its bytes are reached only through
/// <c>IAttachmentStore</c>.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWaydDbContext"/> for the same reason as <see cref="INotificationsDbContext"/>: the
/// modules whose records hold images claim them alongside their own context, and their test fakes should not owe
/// the whole global interface for one set.
/// </remarks>
public interface IAttachmentsDbContext
{
    /// <summary>
    /// Every attachment. Reads are keyed by <c>Id</c>, or by (<c>OwnerType</c>, <c>OwnerId</c>) for a record's
    /// images; <c>OwnerId</c> alone is only unique within an owner type.
    /// </summary>
    /// <remarks>Change attachments through <c>IMarkdownAttachments</c>, which keeps the bytes in step.</remarks>
    DbSet<Attachment> Attachments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
