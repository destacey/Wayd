using Wayd.Common.Application.Discussions;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// A channel type whose answers a test sets directly: which records exist, what the caller may do on them, and who
/// may be mentioned.
/// </summary>
public sealed class FakeDiscussionChannelType(string channelType) : IDiscussionChannelType
{
    private readonly HashSet<Guid> _records = [];

    public string ChannelType { get; } = channelType;

    /// <summary>What every caller may do on every record this type knows. Everything, by default.</summary>
    public DiscussionAccess Access { get; set; } = new(CanReadInternal: true, CanPost: true, CanPostInternal: true, CanModerate: true);

    /// <summary>Employees the author may not mention; every other mention is allowed.</summary>
    public HashSet<Guid> Unmentionable { get; } = [];

    /// <summary>The messages <see cref="OnPosted"/> was called with, in order.</summary>
    public List<DiscussionMessage> Posted { get; } = [];

    /// <summary>Adds a record the caller can read, and returns its id.</summary>
    public Guid AddRecord()
    {
        var id = Guid.NewGuid();
        _records.Add(id);
        return id;
    }

    public Task<DiscussionAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(_records.Contains(ownerId) ? Access : null);

    public Task<IReadOnlySet<Guid>> MentionableAmong(
        Guid ownerId,
        IReadOnlyCollection<Guid> employeeIds,
        DiscussionMessageKind kind,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(employeeIds.Where(id => !Unmentionable.Contains(id)).ToHashSet());

    /// <summary>The mentions <see cref="OnEdited"/> was called with, per message, in order.</summary>
    public List<(DiscussionMessage Message, IReadOnlyCollection<Guid> AddedMentionIds)> Edited { get; } = [];

    public Task OnPosted(DiscussionChannel channel, DiscussionMessage message, CancellationToken cancellationToken)
    {
        Posted.Add(message);
        return Task.CompletedTask;
    }

    public Task OnEdited(DiscussionChannel channel, DiscussionMessage message, IReadOnlyCollection<Guid> addedMentionIds, CancellationToken cancellationToken)
    {
        Edited.Add((message, addedMentionIds));
        return Task.CompletedTask;
    }

    /// <summary>
    /// What every caller may do with the settings of every scope, or null for none at all. Managing, by default.
    /// </summary>
    public DiscussionSettingsAccess? SettingsAccess { get; set; } = new(CanManage: true);

    public Task<DiscussionSettingsAccess?> ResolveSettings(Guid? scopeId, CancellationToken cancellationToken) =>
        Task.FromResult(SettingsAccess);
}
