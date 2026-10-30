using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Employees;

namespace Wayd.Common.Application.Persistence;

/// <summary>
/// The tables of the <c>Discussions</c> schema: every conversation and its messages, across every channel type.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWaydDbContext"/> for the same reason as <see cref="INotificationsDbContext"/>: a
/// module that composes a discussion into one of its own commands reads it alongside its own context, and its test
/// fake should not owe the whole global interface for two sets. <see cref="Employees"/> is here so authors and
/// mentions can be named without that interface too.
/// </remarks>
public interface IDiscussionsDbContext
{
    /// <summary>
    /// Every conversation. Reads are keyed by (<c>ChannelType</c>, <c>OwnerId</c>) — <c>OwnerId</c> alone is only
    /// unique within a channel type.
    /// </summary>
    /// <remarks>Read through this set; change a conversation only through <c>IDiscussionWriter</c>.</remarks>
    DbSet<DiscussionChannel> DiscussionChannels { get; }

    /// <summary>
    /// Every message, top-level and reply. Reads are keyed by <c>ChannelId</c>, then <c>ParentMessageId</c>.
    /// </summary>
    /// <remarks>
    /// Read through this set, and only after the channel type has said what the caller may see: an internal note is
    /// filtered by its <c>Kind</c> in every query. Change a message only through <c>IDiscussionWriter</c>.
    /// </remarks>
    DbSet<DiscussionMessage> DiscussionMessages { get; }

    /// <summary>The shared employee table, for naming authors, deleters and mentioned people.</summary>
    DbSet<Employee> Employees { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
