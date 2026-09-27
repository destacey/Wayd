namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// How a team of teams works under one operating model, as the team of teams operating model events record it.
/// </summary>
/// <param name="TimeZone">The IANA id of the time zone the team of teams' days are counted in.</param>
public sealed record TeamOfTeamsOperatingModelSettings(string TimeZone);
