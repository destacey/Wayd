using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions;

/// <inheritdoc cref="IDiscussionWriter"/>
/// <remarks>
/// Public so Wolverine's generated handlers can construct it directly; nothing outside the discussion commands and
/// module commands composing a post should depend on the class rather than the interface.
/// </remarks>
public sealed class DiscussionWriter(
    IDiscussionsDbContext dbContext,
    IEnumerable<IDiscussionChannelType> channelTypes,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IDiscussionWriter
{
    private readonly IDiscussionsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IDiscussionChannelType> _channelTypes = channelTypes;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc/>
    public async Task<Result<Guid>> Post(PostDiscussionMessage message, CancellationToken cancellationToken)
    {
        var context = await Begin(message.ChannelType, message.OwnerId, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<Guid>(context.Error);
        }

        var (type, access, employeeId) = context.Value;
        if (!access.CanWrite(message.Kind))
        {
            return Result.Failure<Guid>(DiscussionAccessErrors.CannotWrite);
        }

        var now = _dateTimeProvider.Now;
        var actor = Actor(employeeId);

        var channel = await FindChannel(type.ChannelType, message.OwnerId, cancellationToken);
        var opening = channel is null;
        if (channel is null)
        {
            var opened = DiscussionChannel.OpenForRecord(type.ChannelType, message.OwnerId, actor, now);
            if (opened.IsFailure)
            {
                return Result.Failure<Guid>(opened.Error);
            }

            channel = opened.Value;
        }

        var mentions = await Mentionable(type, message.OwnerId, message.Body, message.Kind, cancellationToken);

        var posted = DiscussionMessage.Post(channel, message.Kind, employeeId, message.Subject, message.Body, mentions, actor, now);
        if (posted.IsFailure)
        {
            return Result.Failure<Guid>(posted.Error);
        }

        // Staged only once the message is valid, so a caller that saves its own work after a failed post never
        // stores an empty conversation.
        if (opening)
        {
            _dbContext.DiscussionChannels.Add(channel);
        }

        _dbContext.DiscussionMessages.Add(posted.Value);
        await type.OnPosted(channel, posted.Value, cancellationToken);

        return posted.Value.Id;
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> Reply(string channelType, Guid ownerId, Guid parentMessageId, string body, CancellationToken cancellationToken)
    {
        var context = await Begin(channelType, ownerId, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<Guid>(context.Error);
        }

        var (type, access, employeeId) = context.Value;

        var channel = await FindChannel(type.ChannelType, ownerId, cancellationToken);
        if (channel is null)
        {
            return Result.Failure<Guid>(DiscussionAccessErrors.NotFound);
        }

        var parent = await _dbContext.DiscussionMessages
            .FirstOrDefaultAsync(m => m.Id == parentMessageId && m.ChannelId == channel.Id, cancellationToken);
        if (parent is null || !access.CanRead(parent.Kind))
        {
            return Result.Failure<Guid>(DiscussionAccessErrors.NotFound);
        }

        if (!access.CanWrite(parent.Kind))
        {
            return Result.Failure<Guid>(DiscussionAccessErrors.CannotWrite);
        }

        var mentions = await Mentionable(type, ownerId, body, parent.Kind, cancellationToken);

        var reply = DiscussionMessage.Reply(channel, parent, employeeId, body, mentions, Actor(employeeId), _dateTimeProvider.Now);
        if (reply.IsFailure)
        {
            return Result.Failure<Guid>(reply.Error);
        }

        _dbContext.DiscussionMessages.Add(reply.Value);
        await type.OnPosted(channel, reply.Value, cancellationToken);

        return reply.Value.Id;
    }

    /// <inheritdoc/>
    public async Task<Result> Edit(string channelType, Guid ownerId, Guid messageId, string? subject, string body, CancellationToken cancellationToken)
    {
        var target = await FindMessage(channelType, ownerId, messageId, cancellationToken);
        if (target.IsFailure)
        {
            return Result.Failure(target.Error);
        }

        var (type, access, employeeId, channel, message) = target.Value;

        // Losing the right to write a kind of message ends the right to edit one, even your own.
        if (!access.CanWrite(message.Kind))
        {
            return Result.Failure(DiscussionAccessErrors.CannotWrite);
        }

        // Asked here as well as in the domain so it reads as a refusal, like every other "you may not".
        if (message.AuthorEmployeeId != employeeId)
        {
            return Result.Failure(DiscussionAccessErrors.NotTheAuthor);
        }

        var mentions = await Mentionable(type, ownerId, body, message.Kind, cancellationToken);
        Guid[] added = [.. mentions.Except(message.Mentions.Select(m => m.EmployeeId)).Where(id => id != employeeId)];

        var edited = message.Edit(channel, employeeId, subject, body, mentions, Actor(employeeId), _dateTimeProvider.Now);
        if (edited.IsSuccess && added.Length > 0)
        {
            await type.OnEdited(channel, message, added, cancellationToken);
        }

        return edited;
    }

    /// <inheritdoc/>
    public async Task<Result> Delete(string channelType, Guid ownerId, Guid messageId, CancellationToken cancellationToken)
    {
        var target = await FindMessage(channelType, ownerId, messageId, cancellationToken);
        if (target.IsFailure)
        {
            return Result.Failure(target.Error);
        }

        var (_, access, employeeId, channel, message) = target.Value;

        var mayDelete = access.CanModerate
            || (message.AuthorEmployeeId == employeeId && access.CanWrite(message.Kind));
        if (!mayDelete)
        {
            return Result.Failure(DiscussionAccessErrors.CannotModerate);
        }

        return message.Delete(channel, employeeId, Actor(employeeId), _dateTimeProvider.Now);
    }

    /// <inheritdoc/>
    public async Task<Result> SetLocked(string channelType, Guid ownerId, bool locked, CancellationToken cancellationToken)
    {
        var context = await Begin(channelType, ownerId, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure(context.Error);
        }

        var (type, access, employeeId) = context.Value;
        if (!access.CanModerate)
        {
            return Result.Failure(DiscussionAccessErrors.CannotModerate);
        }

        var now = _dateTimeProvider.Now;
        var actor = Actor(employeeId);

        var channel = await FindChannel(type.ChannelType, ownerId, cancellationToken);
        if (channel is null)
        {
            if (!locked)
            {
                return Result.Failure(DiscussionErrors.NotLocked);
            }

            var opened = DiscussionChannel.OpenForRecord(type.ChannelType, ownerId, actor, now);
            if (opened.IsFailure)
            {
                return Result.Failure(opened.Error);
            }

            channel = opened.Value;
            _dbContext.DiscussionChannels.Add(channel);
        }

        return locked ? channel.Lock(employeeId, actor, now) : channel.Unlock(actor, now);
    }

    /// <inheritdoc/>
    public async Task DeleteChannel(string channelType, Guid ownerId, EventActor actor, CancellationToken cancellationToken)
    {
        // A module passing an unregistered type is a bug, not a request to refuse: its conversations would outlive it.
        var descriptor = DiscussionChannelTypes.Resolve(channelType);
        if (descriptor.IsFailure)
        {
            throw new ArgumentException(descriptor.Error, nameof(channelType));
        }

        var channel = await FindChannel(descriptor.Value.Key, ownerId, cancellationToken);
        if (channel is not null)
        {
            channel.Delete(actor, _dateTimeProvider.Now);

            // The database cascades the channel's messages, their revisions and mentions.
            _dbContext.DiscussionChannels.Remove(channel);
        }
    }

    /// <summary>
    /// Resolves the channel type, the caller's access to the record and their employee id: the start of every change.
    /// </summary>
    private async Task<Result<(IDiscussionChannelType Type, DiscussionAccess Access, Guid EmployeeId)>> Begin(
        string channelType, Guid ownerId, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            LinkedEmployeeRequired.Throw();
        }

        var type = DiscussionChannelTypeResolver.Resolve(_channelTypes, channelType);
        if (type.IsFailure)
        {
            return Result.Failure<(IDiscussionChannelType, DiscussionAccess, Guid)>(type.Error);
        }

        var access = await type.Value.Resolve(ownerId, cancellationToken);
        if (access is null)
        {
            return Result.Failure<(IDiscussionChannelType, DiscussionAccess, Guid)>(DiscussionAccessErrors.NotFound);
        }

        return (type.Value, access, employeeId.Value);
    }

    /// <summary>
    /// The context for changing one existing message: everything <see cref="Begin"/> resolves, plus the channel and
    /// the message, tracked with its mentions and revisions. A message the caller cannot read is not found.
    /// </summary>
    private async Task<Result<(IDiscussionChannelType Type, DiscussionAccess Access, Guid EmployeeId, DiscussionChannel Channel, DiscussionMessage Message)>> FindMessage(
        string channelType, Guid ownerId, Guid messageId, CancellationToken cancellationToken)
    {
        var context = await Begin(channelType, ownerId, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<(IDiscussionChannelType, DiscussionAccess, Guid, DiscussionChannel, DiscussionMessage)>(context.Error);
        }

        var (type, access, employeeId) = context.Value;

        var channel = await FindChannel(type.ChannelType, ownerId, cancellationToken);
        var message = channel is null
            ? null
            : await _dbContext.DiscussionMessages
                .Include(m => m.Mentions)
                .Include(m => m.Revisions)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ChannelId == channel.Id, cancellationToken);

        if (channel is null || message is null || !access.CanRead(message.Kind))
        {
            return Result.Failure<(IDiscussionChannelType, DiscussionAccess, Guid, DiscussionChannel, DiscussionMessage)>(DiscussionAccessErrors.NotFound);
        }

        return (type, access, employeeId, channel, message);
    }

    private Task<DiscussionChannel?> FindChannel(string channelType, Guid ownerId, CancellationToken cancellationToken) =>
        _dbContext.DiscussionChannels.FirstOrDefaultAsync(c => c.ChannelType == channelType && c.OwnerId == ownerId, cancellationToken);

    private async Task<IReadOnlySet<Guid>> Mentionable(
        IDiscussionChannelType type, Guid ownerId, string? body, DiscussionMessageKind kind, CancellationToken cancellationToken)
    {
        var mentioned = DiscussionMentions.Parse(body);
        return mentioned.Count == 0
            ? new HashSet<Guid>()
            : await type.MentionableAmong(ownerId, mentioned, kind, cancellationToken);
    }

    private EventActor Actor(Guid employeeId) => EventActor.User(_currentUser.GetUserId(), employeeId);
}
