using Wayd.Organization.Application.TeamsOfTeams.Commands;

namespace Wayd.Web.Api.Models.Organizations.TeamsOfTeams;

public sealed record UpdateTeamOfTeamsOperatingModelRequest
{
    /// <summary>
    /// The IANA id of the time zone the team of teams' own rollups count days in.
    /// </summary>
    public string TimeZone { get; set; } = default!;

    public UpdateTeamOfTeamsOperatingModelCommand ToUpdateTeamOfTeamsOperatingModelCommand(Guid teamId, Guid operatingModelId)
    {
        return new UpdateTeamOfTeamsOperatingModelCommand(teamId, operatingModelId, TimeZone);
    }
}

public sealed class UpdateTeamOfTeamsOperatingModelRequestValidator : CustomValidator<UpdateTeamOfTeamsOperatingModelRequest>
{
    public UpdateTeamOfTeamsOperatingModelRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.TimeZone)
            .NotEmpty()
            .IsIanaTimeZone();
    }
}
