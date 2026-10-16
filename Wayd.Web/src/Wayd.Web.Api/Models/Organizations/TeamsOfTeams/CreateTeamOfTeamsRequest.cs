using Wayd.Common.Domain.Extensions.Organizations;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.TeamsOfTeams.Commands;

namespace Wayd.Web.Api.Models.Organizations.TeamsOfTeams;

public sealed record CreateTeamOfTeamsRequest
{
    /// <summary>Gets the team name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets the code.</summary>
    public string Code { get; set; } = default!;

    /// <summary>Gets the team description.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The active date for the team.
    /// </summary>
    public LocalDate ActiveDate { get; set; }

    public CreateTeamOfTeamsCommand ToCreateTeamOfTeamsCommand()
    {
        TeamCode code = (TeamCode)Code;

        return new CreateTeamOfTeamsCommand(Name, code, Description, ActiveDate);
    }
}

public sealed class CreateTeamOfTeamsRequestValidator : CustomValidator<CreateTeamOfTeamsRequest>
{
    public CreateTeamOfTeamsRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Code)
            .MinimumLength(2)
            .MaximumLength(10)
            .Must(t => t.IsValidTeamCodeFormat())
                .WithMessage("Invalid code format. Team codes are uppercase letters and numbers only, 2-10 characters.");

        RuleFor(t => t.Description)
            .MaximumLength(1024);

        RuleFor(t => t.ActiveDate)
            .NotEmpty();
    }
}
