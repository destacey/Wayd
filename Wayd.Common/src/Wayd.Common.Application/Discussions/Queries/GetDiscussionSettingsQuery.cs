using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Queries;

/// <summary>
/// The discussion settings in effect for one scope, such as an intake desk, or for a channel type's default when
/// <see cref="ScopeId"/> is null. Null when the scope does not exist or the caller cannot see it.
/// </summary>
/// <param name="ChannelType">The registered channel type.</param>
/// <param name="ScopeId">The scope's record, or null for the channel type's default.</param>
public sealed record GetDiscussionSettingsQuery(string ChannelType, Guid? ScopeId) : IQuery<Result<DiscussionSettingsDto?>>;

/// <summary>Rejects a missing channel type, and an empty scope id where null was meant.</summary>
public sealed class GetDiscussionSettingsQueryValidator : CustomValidator<GetDiscussionSettingsQuery>
{
    public GetDiscussionSettingsQueryValidator()
    {
        RuleFor(q => q.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.ScopeId).NotEqual(Guid.Empty);
    }
}

/// <summary>Asks the channel type who may see the settings, then reads the ones in effect.</summary>
public sealed class GetDiscussionSettingsQueryHandler(
    IDiscussionsDbContext dbContext,
    IEnumerable<IDiscussionChannelType> channelTypes)
    : IQueryHandler<GetDiscussionSettingsQuery, Result<DiscussionSettingsDto?>>
{
    private readonly IDiscussionsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IDiscussionChannelType> _channelTypes = channelTypes;

    public async Task<Result<DiscussionSettingsDto?>> Handle(GetDiscussionSettingsQuery request, CancellationToken cancellationToken)
    {
        var type = DiscussionChannelTypeResolver.Resolve(_channelTypes, request.ChannelType);
        if (type.IsFailure)
        {
            return Result.Failure<DiscussionSettingsDto?>(type.Error);
        }

        var access = await type.Value.ResolveSettings(request.ScopeId, cancellationToken);
        if (access is null)
        {
            return Result.Success<DiscussionSettingsDto?>(null);
        }

        var descriptor = DiscussionChannelTypes.Resolve(type.Value.ChannelType).Value;
        var settings = await _dbContext.DiscussionSettingsFor(descriptor.Key, request.ScopeId, cancellationToken);

        var id = DiscussionSettings.IdFor(descriptor.Key, request.ScopeId);
        var hasOwn = await _dbContext.DiscussionSettings.AnyAsync(s => s.Id == id, cancellationToken);

        return new DiscussionSettingsDto
        {
            ChannelType = descriptor.Key,
            ScopeId = request.ScopeId,
            InternalNotesEnabled = settings.InternalNotesEnabled,
            MentionsGrantAccess = settings.MentionsGrantAccess,
            IsInherited = !hasOwn,
            SupportsInternalNotes = descriptor.SupportsInternalNotes,
            SupportsMentionAccess = descriptor.SupportsMentionAccess,
            CanManage = access.CanManage,
        };
    }
}
