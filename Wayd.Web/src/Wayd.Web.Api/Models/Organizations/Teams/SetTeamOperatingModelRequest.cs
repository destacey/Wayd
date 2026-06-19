using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Domain.Enums;

namespace Wayd.Web.Api.Models.Organizations.Teams;

public sealed record SetTeamOperatingModelRequest
{
    /// <summary>
    /// The start date for this operating model.
    /// </summary>
    public LocalDate StartDate { get; set; }

    /// <summary>
    /// The methodology the team uses (e.g., Scrum, Kanban).
    /// </summary>
    public Methodology Methodology { get; set; }

    /// <summary>
    /// The sizing method the team uses (e.g., StoryPoints, Count).
    /// </summary>
    public SizingMethod SizingMethod { get; set; }

    public SetTeamOperatingModelCommand ToSetTeamOperatingModelCommand(Guid teamId)
    {
        return new SetTeamOperatingModelCommand(teamId, StartDate, Methodology, SizingMethod);
    }
}

public sealed class SetTeamOperatingModelRequestValidator : CustomValidator<SetTeamOperatingModelRequest>
{
    public SetTeamOperatingModelRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.StartDate)
            .NotEmpty();

        RuleFor(r => r.Methodology)
            .IsInEnum();

        RuleFor(r => r.SizingMethod)
            .IsInEnum();
    }
}
