using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;

namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// The saved settings for one channel type in one scope — an intake desk, say — or, with no scope, the channel
/// type's default for every scope that has saved none.
/// </summary>
/// <remarks>
/// A record's conversation uses its scope's row, else its channel type's default row, else
/// <see cref="DiscussionChannelTypeDescriptor.DefaultSettings"/>. The id is derived from the channel type and scope,
/// so each has exactly one possible row and two first saves at once collide on the key. A row whose values come to
/// equal what its scope would inherit is removed, so the scope follows its default again.
/// </remarks>
public sealed class DiscussionSettings : BaseEntity<Guid>
{
    // Changing this gives every row a new id, orphaning it and its activity history.
    private static readonly Guid _idNamespace = new("6a0c2f4e-9b3d-4e71-8c55-2d7f1b9e4a63");

    private DiscussionSettings() { }

    private DiscussionSettings(string channelType, Guid? scopeId, DiscussionSettingValues values)
    {
        Id = IdFor(channelType, scopeId);
        ChannelType = channelType;
        ScopeId = scopeId;
        Apply(values);
    }

    /// <summary>The registered channel type these settings apply to.</summary>
    public string ChannelType { get; private init; } = default!;

    /// <summary>The scope's record, such as an intake desk's id, or null for the channel type's default.</summary>
    public Guid? ScopeId { get; private init; }

    /// <summary>Whether new internal notes can be posted.</summary>
    public bool InternalNotesEnabled { get; private set; }

    /// <summary>Whether a mention can give the person mentioned access to the record.</summary>
    public bool MentionsGrantAccess { get; private set; }

    /// <summary>The settings as values.</summary>
    public DiscussionSettingValues Values => new(InternalNotesEnabled, MentionsGrantAccess);

    /// <summary>The id of the row for a channel type and scope, whether or not it exists.</summary>
    /// <remarks>The channel type must be the registered spelling, or the id differs by casing.</remarks>
    public static Guid IdFor(string channelType, Guid? scopeId) =>
        NameBasedUuid.Create(_idNamespace, $"{channelType}:{scopeId?.ToString() ?? "default"}");

    /// <summary>
    /// Saves settings for a scope, or a channel type's default, that has so far inherited
    /// <paramref name="inherited"/>. Null when <paramref name="values"/> are those, so a save that changes nothing
    /// does not pin them: a scope with no row keeps following its default as it changes.
    /// </summary>
    public static Result<DiscussionSettings?> Create(
        DiscussionChannelTypeDescriptor descriptor,
        Guid? scopeId,
        DiscussionSettingValues inherited,
        DiscussionSettingValues values,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(descriptor, nameof(descriptor));
        Guard.Against.Null(inherited, nameof(inherited));
        Guard.Against.Null(actor, nameof(actor));

        var invalid = Validate(descriptor, scopeId, values);
        if (invalid.IsFailure)
        {
            return Result.Failure<DiscussionSettings?>(invalid.Error);
        }

        if (values == inherited)
        {
            return Result.Success<DiscussionSettings?>(null);
        }

        var settings = new DiscussionSettings(descriptor.Key, scopeId, values);
        settings.Raise(descriptor, inherited, actor, timestamp);

        return settings;
    }

    /// <summary>
    /// Replaces the saved settings. Returns whether anything in effect changed; the previous values are the ones that
    /// took effect, so a saved value the channel type no longer supports is recorded as off.
    /// </summary>
    public Result<bool> ChangeValues(DiscussionChannelTypeDescriptor descriptor, DiscussionSettingValues values, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(descriptor, nameof(descriptor));
        Guard.Against.Null(actor, nameof(actor));

        if (!string.Equals(descriptor.Key, ChannelType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"These settings belong to '{ChannelType}', not '{descriptor.Key}'.", nameof(descriptor));
        }

        var invalid = Validate(descriptor, ScopeId, values);
        if (invalid.IsFailure)
        {
            return Result.Failure<bool>(invalid.Error);
        }

        var before = descriptor.Supported(Values);
        Apply(values);
        if (Values == before)
        {
            return false;
        }

        Raise(descriptor, before, actor, timestamp);
        return true;
    }

    /// <summary>
    /// Records that a scope's settings are being removed because the scope is being deleted. The caller then removes
    /// the row. A channel type's default has no scope to be deleted with, so it is never removed this way.
    /// </summary>
    /// <exception cref="InvalidOperationException">These are a channel type's default settings.</exception>
    public void Delete(DiscussionChannelTypeDescriptor descriptor, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(descriptor, nameof(descriptor));
        Guard.Against.Null(actor, nameof(actor));

        if (ScopeId is not { } scopeId)
        {
            throw new InvalidOperationException("A channel type's default settings have no scope to be deleted with.");
        }

        AddDomainEvent(new DiscussionSettingsDeletedEvent(Id, ChannelType, descriptor.SettingsScopeType!, scopeId, actor, timestamp));
    }

    private static Result Validate(DiscussionChannelTypeDescriptor descriptor, Guid? scopeId, DiscussionSettingValues values)
    {
        Guard.Against.Null(values, nameof(values));

        if (!descriptor.SupportsSettings)
        {
            return Result.Failure(DiscussionErrors.SettingsNotSupported);
        }

        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException("A scope id cannot be empty; pass null for the channel type's default.", nameof(scopeId));
        }

        if (values.InternalNotesEnabled && !descriptor.SupportsInternalNotes)
        {
            return Result.Failure(DiscussionErrors.InternalNotesNotSupported);
        }

        if (values.MentionsGrantAccess && !descriptor.SupportsMentionAccess)
        {
            return Result.Failure(DiscussionErrors.MentionAccessNotSupported);
        }

        return Result.Success();
    }

    private void Apply(DiscussionSettingValues values)
    {
        InternalNotesEnabled = values.InternalNotesEnabled;
        MentionsGrantAccess = values.MentionsGrantAccess;
    }

    private void Raise(DiscussionChannelTypeDescriptor descriptor, DiscussionSettingValues before, EventActor actor, Instant timestamp) =>
        AddDomainEvent(new DiscussionSettingsValuesChangedEvent(
            Id,
            ChannelType,
            descriptor.SettingsScopeType!,
            ScopeId,
            before.InternalNotesEnabled,
            InternalNotesEnabled,
            before.MentionsGrantAccess,
            MentionsGrantAccess,
            actor,
            timestamp));
}
