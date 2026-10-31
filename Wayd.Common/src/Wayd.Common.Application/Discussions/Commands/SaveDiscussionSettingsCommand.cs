using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Commands;

/// <summary>
/// Saves how conversations behave in one scope, such as an intake desk, or by default for a channel type when
/// <see cref="ScopeId"/> is null. Each change is recorded with both ends in the Activity log.
/// </summary>
/// <param name="ChannelType">The registered channel type.</param>
/// <param name="ScopeId">The scope's record, or null for the channel type's default.</param>
/// <param name="InternalNotesEnabled">Whether new internal notes can be posted.</param>
/// <param name="MentionsGrantAccess">Whether a mention can give the person mentioned access to the record.</param>
public sealed record SaveDiscussionSettingsCommand(
    string ChannelType,
    Guid? ScopeId,
    bool InternalNotesEnabled,
    bool MentionsGrantAccess) : ICommand;

/// <summary>Rejects a missing channel type and an empty scope id.</summary>
public sealed class SaveDiscussionSettingsCommandValidator : CustomValidator<SaveDiscussionSettingsCommand>
{
    public SaveDiscussionSettingsCommandValidator()
    {
        RuleFor(c => c.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.ScopeId).NotEqual(Guid.Empty);
    }
}

/// <summary>
/// Asks the channel type whether the caller runs the scope, then creates, changes or removes its settings row and
/// saves. A row is kept only while its values differ from what the scope would inherit.
/// </summary>
public sealed class SaveDiscussionSettingsCommandHandler(
    IDiscussionsDbContext dbContext,
    IEnumerable<IDiscussionChannelType> channelTypes,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SaveDiscussionSettingsCommand>
{
    private readonly IDiscussionsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IDiscussionChannelType> _channelTypes = channelTypes;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SaveDiscussionSettingsCommand command, CancellationToken cancellationToken)
    {
        var type = DiscussionChannelTypeResolver.Resolve(_channelTypes, command.ChannelType);
        if (type.IsFailure)
        {
            return Result.Failure(type.Error);
        }

        var access = await type.Value.ResolveSettings(command.ScopeId, cancellationToken);
        if (access is null)
        {
            return Result.Failure(DiscussionAccessErrors.NotFound);
        }

        if (!access.CanManage)
        {
            return Result.Failure(DiscussionAccessErrors.CannotManageSettings);
        }

        var descriptor = DiscussionChannelTypes.Resolve(type.Value.ChannelType).Value;
        var values = new DiscussionSettingValues(command.InternalNotesEnabled, command.MentionsGrantAccess);
        var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
        var now = _dateTimeProvider.Now;

        var id = DiscussionSettings.IdFor(descriptor.Key, command.ScopeId);
        var saved = await _dbContext.DiscussionSettings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        var inherited = await _dbContext.InheritedDiscussionSettingsFor(descriptor.Key, command.ScopeId, cancellationToken);

        if (saved is null)
        {
            var created = DiscussionSettings.Create(descriptor, command.ScopeId, inherited, values, actor, now);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            if (created.Value is null)
            {
                return Result.Success();
            }

            _dbContext.DiscussionSettings.Add(created.Value);
        }
        else
        {
            var changed = saved.ChangeValues(descriptor, values, actor, now);
            if (changed.IsFailure)
            {
                return Result.Failure(changed.Error);
            }

            // Back to what it would inherit: drop the row so the scope follows its default again. A deleted row's
            // events are still captured on save, so the change reaches the Activity log.
            if (values == inherited)
            {
                _dbContext.DiscussionSettings.Remove(saved);
            }
            else if (!changed.Value)
            {
                return Result.Success();
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two first saves for one scope meet at the key; two changes at the row version.
            return Result.Failure(DiscussionAccessErrors.Conflict);
        }

        return Result.Success();
    }
}
