using Wayd.Common.Domain.Interfaces.Organization;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The current state of the Organization teams and teams of teams named by id or by code, each listed once
/// whichever way it was named. A value that names no team is absent from the answer, as is one that is not a
/// well-formed code.
/// </summary>
/// <remarks>
/// For a module that lets people name teams in bulk — a file names a team by its code, which a spreadsheet
/// holds — while keeping its own copy of teams keyed by id. Organization is the source of truth: a module's
/// copy can lag a rename or a deactivation. Inactive teams are included, so the caller can say why one
/// cannot be used.
/// </remarks>
public sealed record GetTeamsByIdOrCodeQuery(IReadOnlyCollection<Guid> Ids, IReadOnlyCollection<string> Codes)
    : IQuery<IReadOnlyList<ISimpleTeam>>;
