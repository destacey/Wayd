using Wayd.Organization.Application.TeamsOfTeams.Commands;

namespace Wayd.Web.Api.Models.Organizations.TeamsOfTeams;

public sealed record SetTeamOfTeamsOperatingModelRequest
{
    /// <summary>
    /// The start date for this operating model.
    /// </summary>
    public LocalDate StartDate { get; set; }

    /// <summary>
    /// The IANA id of the time zone the team of teams' own rollups count days in.
    /// </summary>
    public string TimeZone { get; set; } = default!;

    public SetTeamOfTeamsOperatingModelCommand ToSetTeamOfTeamsOperatingModelCommand(Guid teamId)
    {
        return new SetTeamOfTeamsOperatingModelCommand(teamId, StartDate, TimeZone);
    }
}

public sealed class SetTeamOfTeamsOperatingModelRequestValidator : CustomValidator<SetTeamOfTeamsOperatingModelRequest>
{
    public SetTeamOfTeamsOperatingModelRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.StartDate)
            .NotEmpty();

        RuleFor(r => r.TimeZone)
            .NotEmpty()
            .IsIanaTimeZone();
    }
}
