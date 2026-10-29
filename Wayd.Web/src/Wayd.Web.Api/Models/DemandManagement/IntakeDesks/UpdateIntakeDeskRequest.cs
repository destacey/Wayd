using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// An intake desk's settings and members, overwriting what it has. Settings that do not change record nothing.
/// </summary>
public sealed record UpdateIntakeDeskRequest
{
    /// <summary>
    /// The desk being changed; must match the route.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What its members and administrators call the desk. Unique.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The requester-facing description of what kind of ask to bring here, which requesters choose by.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// How much of a request on the desk reaches people outside it: Open, SummaryOnly or Private. Only the
    /// Demand Management Admin may change it.
    /// </summary>
    public IntakeDeskVisibility Visibility { get; set; }

    /// <summary>
    /// Whether a requester can pick the desk. One they cannot is a destination agents route to.
    /// </summary>
    public bool IsRequesterSelectable { get; set; }

    /// <summary>
    /// Whether the desk accepts new requests.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// On a private desk, whether the requester may add participants themselves. Agents always can.
    /// </summary>
    public bool RequesterMayAddParticipants { get; set; }

    /// <summary>
    /// Whether the desk offers every active company channel, including channels added later. A new desk does.
    /// Leave it out to keep the desk's channels as they are.
    /// </summary>
    public bool? OffersAllChannels { get; set; }

    /// <summary>
    /// The company channels the desk offers when <c>offersAllChannels</c> is false; empty means the desk offers
    /// none, and its submit form does not ask where a request came from. Leave it out to keep the ones it has.
    /// Ignored unless <c>offersAllChannels</c> is false. A channel already chosen may stay after it is archived;
    /// a newly chosen one must be active.
    /// </summary>
    public List<Guid>? ChannelIds { get; set; }

    /// <summary>
    /// Whether the desk's agents tag requests along every active company tag axis, including axes added later. A
    /// new desk does. Leave it out to keep the desk's axes as they are.
    /// </summary>
    public bool? OffersAllTagCategories { get; set; }

    /// <summary>
    /// The tag axes the desk's agents tag along when <c>offersAllTagCategories</c> is false; empty means none. Leave
    /// it out to keep the ones it has. Ignored unless <c>offersAllTagCategories</c> is false. An axis already chosen
    /// may stay after it is deactivated; a newly chosen one must be active. Requests keep tags along any axis.
    /// </summary>
    public List<Guid>? TagCategoryIds { get; set; }

    /// <summary>
    /// Whether the desk's agents may add free tags — tags on no axis, typed in while tagging. A new desk does not.
    /// Leave it out to keep the desk's setting. Requests already carrying free tags keep them when it is turned off.
    /// </summary>
    public bool? AllowsFreeTags { get; set; }

    /// <summary>
    /// Everyone on the desk after the change, each with their role. Naming, removing or re-roling a Desk Admin
    /// needs the Demand Management Admin.
    /// </summary>
    public List<IntakeDeskMemberRequest> Members { get; set; } = [];

    /// <summary>
    /// The desk's <c>revision</c> as it was read before making this change. A desk changed since refuses the
    /// update with 409, so a stale page never undoes what someone else changed.
    /// </summary>
    public int ExpectedRevision { get; set; }

    public UpdateIntakeDeskCommand ToUpdateIntakeDeskCommand() =>
        new(Id, Name, Description, Visibility, IsRequesterSelectable, IsOpen, RequesterMayAddParticipants,
            OffersAllChannels, ChannelIds, [.. Members.Select(m => m.ToMemberDetails())], ExpectedRevision,
            OffersAllTagCategories, TagCategoryIds, AllowsFreeTags);
}

public sealed class UpdateIntakeDeskRequestValidator : CustomValidator<UpdateIntakeDeskRequest>
{
    public UpdateIntakeDeskRequestValidator()
    {
        RuleFor(d => d.Id).NotEmpty();
        RuleFor(d => d.Name).NotEmpty().MaximumLength(128);
        RuleFor(d => d.Description).NotEmpty().MaximumLength(1024);
        RuleFor(d => d.Visibility).IsInEnum();
        RuleForEach(d => d.ChannelIds).NotEmpty();
        RuleForEach(d => d.TagCategoryIds).NotEmpty();
        RuleFor(d => d.Members).NotNull()
            .Must(members => members.Select(m => m.EmployeeId).Distinct().Count() == members.Count)
            .WithMessage("An employee can hold only one role on a desk.");
        RuleForEach(d => d.Members).SetValidator(new IntakeDeskMemberRequestValidator());
    }
}
