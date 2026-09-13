namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// A structural view of the generated delivery hierarchy, exposed so the PPM generator can hang portfolios,
/// programs and projects off the same value streams, ARTs and teams the org is built from — and staff them
/// from the people already on those teams. Everything is keyed by the same natural keys the CSV imports use
/// (team codes, employee numbers), so the PPM rows line up with the org rows without sharing generated Ids.
/// </summary>
/// <param name="ValueStreams">The delivery hierarchy, staffed with the people in post today.</param>
/// <param name="ChiefExecutiveEmployeeNumber">
/// The executives, named so the user generator can give a seeded environment a way in that does not depend
/// on PPM having been generated. Optional because a structure can be built without an executive layer.
/// </param>
/// <param name="Positions">
/// Everyone who has held each position, keyed by the employee number of every one of them. Absent for a
/// structure built by hand, which then has no history: each person has always held their position.
/// </param>
public sealed record OrgStructure(
    IReadOnlyList<ValueStreamNode> ValueStreams,
    string? ChiefExecutiveEmployeeNumber = null,
    string? ChiefTechnologyEmployeeNumber = null,
    string? ChiefProductEmployeeNumber = null,
    IReadOnlyDictionary<string, IReadOnlyList<Tenure>>? Positions = null)
{
    /// <summary>
    /// Who held the position <paramref name="employeeNumber"/> holds on a given day.
    /// </summary>
    /// <remarks>
    /// The hierarchy names today's people, so a record dated in the past asks here for whoever was in post
    /// then. In the weeks between someone leaving and their replacement starting, that is the person who
    /// left — the position's work was still theirs to hand over. Before the position's first holder started
    /// it is that first holder, since nobody earlier existed to name.
    /// </remarks>
    public string HolderOn(string employeeNumber, DateOnly on)
    {
        if (Positions is null || !Positions.TryGetValue(employeeNumber, out var holders))
            return employeeNumber;

        var holder = holders[0];
        foreach (var tenure in holders)
        {
            if (tenure.HiredOn > on)
                break;

            holder = tenure;
        }

        return holder.EmployeeNumber;
    }

    /// <summary>When someone held their position, or null for a structure with no history.</summary>
    public Tenure? TenureOf(string employeeNumber) =>
        Positions is not null && Positions.TryGetValue(employeeNumber, out var holders)
            ? holders.First(t => string.Equals(t.EmployeeNumber, employeeNumber, StringComparison.OrdinalIgnoreCase))
            : null;
}

/// <summary>
/// One person's time in a position. <see cref="LeftOn"/> is null for whoever holds it today.
/// </summary>
/// <remarks>
/// Known only to the generator. An employee record carries a hire date and whether they are active, and
/// nothing in Wayd needs the day someone left — but generating a completed project owned by someone who had
/// already gone, or an open one owned by anyone inactive, is exactly what this exists to prevent.
/// </remarks>
public sealed record Tenure(string EmployeeNumber, DateOnly HiredOn, DateOnly? LeftOn);

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

/// <summary>
/// An ART (mid tier) grouping delivery teams, with its engineering and product leads — or, when the ART tier
/// is switched off, the value stream's teams as the one group that plans and ships together. That group has
/// no team of teams of its own, so <see cref="TeamCode"/> and both leads are null and <see cref="Name"/> is
/// the value stream's.
/// </summary>
public sealed record ArtNode(
    string? TeamCode,
    string Name,
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
