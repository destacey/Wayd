using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions;

/// <summary>
/// The settings in effect for conversations in one scope: its saved row, else its channel type's default row, else
/// the channel type's own defaults.
/// </summary>
/// <remarks>
/// Every reader of a setting asks here, so a scope that has never saved any follows its default as that changes. A
/// saved value the channel type can no longer support — internal notes on a type that dropped them — reads as off.
/// Extensions on the context rather than a service, so the writer and the module channel types that need settings
/// share one reading without one service depending on another.
/// </remarks>
public static class DiscussionSettingsReading
{
    /// <summary>The settings in effect for a channel type in a scope, or its default when <paramref name="scopeId"/> is null.</summary>
    /// <exception cref="ArgumentException">The channel type is not registered.</exception>
    public static async Task<DiscussionSettingValues> DiscussionSettingsFor(
        this IDiscussionsDbContext dbContext, string channelType, Guid? scopeId, CancellationToken cancellationToken)
    {
        var descriptor = Descriptor(channelType);
        if (!descriptor.SupportsSettings)
        {
            return descriptor.DefaultSettings;
        }

        var rows = await Rows(dbContext, descriptor.Key, scopeId, cancellationToken);
        var saved = rows.FirstOrDefault(r => scopeId is not null && r.ScopeId == scopeId)
            ?? rows.FirstOrDefault(r => r.ScopeId is null);

        return descriptor.Supported(saved?.Values ?? descriptor.DefaultSettings);
    }

    /// <summary>
    /// The settings a scope inherits when it has saved none: its channel type's default row, else the channel type's
    /// own defaults. For a channel type's default itself, always the channel type's own defaults.
    /// </summary>
    /// <exception cref="ArgumentException">The channel type is not registered.</exception>
    public static async Task<DiscussionSettingValues> InheritedDiscussionSettingsFor(
        this IDiscussionsDbContext dbContext, string channelType, Guid? scopeId, CancellationToken cancellationToken)
    {
        var descriptor = Descriptor(channelType);
        if (!descriptor.SupportsSettings || scopeId is null)
        {
            return descriptor.DefaultSettings;
        }

        var rows = await Rows(dbContext, descriptor.Key, null, cancellationToken);
        var saved = rows.FirstOrDefault(r => r.ScopeId is null);

        return descriptor.Supported(saved?.Values ?? descriptor.DefaultSettings);
    }

    private static DiscussionChannelTypeDescriptor Descriptor(string channelType)
    {
        var descriptor = DiscussionChannelTypes.Resolve(channelType);
        return descriptor.IsSuccess
            ? descriptor.Value
            : throw new ArgumentException(descriptor.Error, nameof(channelType));
    }

    /// <summary>The scope's row and the channel type's default row, whichever exist.</summary>
    private static async Task<List<SavedRow>> Rows(
        IDiscussionsDbContext dbContext, string key, Guid? scopeId, CancellationToken cancellationToken) =>
        await dbContext.DiscussionSettings
            .Where(s => s.ChannelType == key && (s.ScopeId == null || s.ScopeId == scopeId))
            .Select(s => new SavedRow(
                s.ScopeId, new DiscussionSettingValues(s.InternalNotesEnabled, s.MentionsGrantAccess)))
            .ToListAsync(cancellationToken);

    private sealed record SavedRow(Guid? ScopeId, DiscussionSettingValues Values);
}
