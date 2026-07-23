namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// A structural view of the generated delivery hierarchy, exposed so the PPM generator can hang portfolios,
/// programs and projects off the same value streams, ARTs and teams the org is built from — and staff them
/// from the people already on those teams. Everything is keyed by the same natural keys the CSV imports use
/// (team codes, employee numbers), so the PPM rows line up with the org rows without sharing generated Ids.
/// </summary>
public sealed record OrgStructure(IReadOnlyList<ValueStreamNode> ValueStreams);

/// <summary>
/// A value stream (top of the delivery hierarchy). In a small org this may be a single ART with no separate
/// value-stream team of teams, in which case <see cref="EngineeringLeadEmployeeNumber"/> is null.
/// </summary>
public sealed record ValueStreamNode(
    string Domain,
    string? TeamCode,
    string? EngineeringLeadEmployeeNumber,
    string? ProductLeadEmployeeNumber,
    IReadOnlyList<ArtNode> Arts);

/// <summary>An ART (mid tier) grouping delivery teams, with its engineering and product leads.</summary>
public sealed record ArtNode(
    string TeamCode,
    string? EngineeringLeadEmployeeNumber,
    string? ProductLeadEmployeeNumber,
    IReadOnlyList<TeamNode> Teams);

/// <summary>
/// A leaf delivery team. Carries the natural keys the PPM generator staffs projects from: the engineering
/// manager, the product owner, and every member's employee number (managers included).
/// </summary>
public sealed record TeamNode(
    string TeamCode,
    string Name,
    string? EngineeringManagerEmployeeNumber,
    string? ProductOwnerEmployeeNumber,
    IReadOnlyList<string> MemberEmployeeNumbers);
