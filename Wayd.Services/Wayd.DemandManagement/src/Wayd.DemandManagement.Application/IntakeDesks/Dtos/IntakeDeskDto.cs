using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// An intake desk: what it takes, who works it, and how much of its requests the rest of the company reads.
/// </summary>
public sealed record IntakeDeskDto
{
    /// <summary>The desk's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The desk's key.</summary>
    public int Key { get; init; }

    /// <summary>What its members and administrators call the desk.</summary>
    public required string Name { get; init; }

    /// <summary>The requester-facing description of what kind of ask to bring here, which requesters choose by.</summary>
    public required string Description { get; init; }

    /// <summary>How much of a request on this desk reaches people outside it.</summary>
    public IntakeDeskVisibility Visibility { get; init; }

    /// <summary>Whether a requester can pick this desk.</summary>
    public bool IsRequesterSelectable { get; init; }

    /// <summary>Whether the desk accepts new requests.</summary>
    public bool IsOpen { get; init; }

    /// <summary>On a private desk, whether the requester may add participants themselves.</summary>
    public bool RequesterMayAddParticipants { get; init; }

    /// <summary>Whether the desk offers every active company channel, including channels added later.</summary>
    public bool OffersAllChannels { get; init; }

    /// <summary>
    /// The company channels the desk chose, in the company's order, while it does not offer them all; an
    /// archived one stays chosen but is not offered. Empty with <see cref="OffersAllChannels"/> false means the
    /// desk offers none.
    /// </summary>
    public required IReadOnlyList<Guid> ChannelIds { get; init; }

    /// <summary>Whether the desk's agents tag along every active company tag axis, including axes added later.</summary>
    public bool OffersAllTagCategories { get; init; }

    /// <summary>
    /// The tag axes the desk chose, in the company's order, while it does not offer them all; an inactive one
    /// stays chosen but is not offered. Empty with <see cref="OffersAllTagCategories"/> false means its agents tag
    /// along none.
    /// </summary>
    public IReadOnlyList<Guid> TagCategoryIds { get; init; } = [];

    /// <summary>How many changes the desk has recorded; sent back with a change so a stale one is refused.</summary>
    public int Revision { get; init; }

    /// <summary>The employees named on the desk, by name, with their roles.</summary>
    public required IReadOnlyList<IntakeDeskMemberDto> Members { get; init; }

    /// <summary>
    /// What is waiting on the desk, for its members and the Demand Management Admin; null for anyone else,
    /// so a private desk's numbers never reach someone outside it.
    /// </summary>
    public IntakeDeskWaitingDto? Waiting { get; init; }

    /// <summary>Whether the reader may change the desk's details, settings and agents: a Desk Admin on it.</summary>
    public bool CanManage { get; init; }

    /// <summary>
    /// Whether the reader may change the desk's visibility and its Desk Admins: the Demand Management Admin.
    /// </summary>
    public bool CanAdminister { get; init; }

    /// <summary>Whether the reader may delete the desk: the Demand Management Admin, and only while no request is on it.</summary>
    public bool CanDelete { get; init; }
}
