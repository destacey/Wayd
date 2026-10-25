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

    /// <summary>The employees named on the desk, by name, with their roles.</summary>
    public required IReadOnlyList<IntakeDeskMemberDto> Members { get; init; }
}
