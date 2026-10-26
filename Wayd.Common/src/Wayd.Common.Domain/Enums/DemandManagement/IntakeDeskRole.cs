using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// What a member of an intake desk may do on it. Membership is the grant: no claim is needed beside it.
/// </summary>
/// <remarks>
/// Generic on purpose — an agent may review, approve or do whatever the desk's process asks — so the role
/// never reads as anyone's job title. The Demand Management Admin acts as <see cref="Admin"/> on every desk
/// without being a member.
/// </remarks>
public enum IntakeDeskRole
{
    /// <summary>
    /// Runs the desk: everything an agent does, plus its details, settings and agents, and deleting its requests.
    /// </summary>
    [Display(Name = "Desk Admin", Description = "Runs the desk: changes its details and settings, names its agents, deletes its requests, and does everything an agent does.", Order = 1)]
    Admin = 1,

    /// <summary>
    /// Acts on the desk's requests: reads them in full, edits, assigns, re-routes and manages participants.
    /// </summary>
    [Display(Name = "Agent", Description = "Acts on the desk's requests: reads them in full, edits them, assigns them, moves them to another desk and manages their participants.", Order = 2)]
    Agent = 2,
}
