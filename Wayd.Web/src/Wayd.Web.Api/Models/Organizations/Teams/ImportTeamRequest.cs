using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Extensions.Organizations;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Dtos;
using NodaTime.Extensions;

namespace Wayd.Web.Api.Models.Organizations.Teams;

/// <summary>
/// A single CSV row for the unified team import. <see cref="Type"/> discriminates between a Team and a
/// Team of Teams (case-insensitive: "Team" / "TeamOfTeams"). Both share the same create shape.
/// </summary>
public sealed class ImportTeamRequest
{
    public string Type { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime ActiveDate { get; set; }

    /// <summary>Whether the team is currently active. Defaults to true when the column is absent.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the team was retired. Required when <see cref="IsActive"/> is false; must be after <see cref="ActiveDate"/>.</summary>
    public DateTime? InactiveDate { get; set; }

    public ImportTeamDto ToImportTeamDto()
    {
        var teamType = Enum.Parse<TeamType>(Type.Trim(), ignoreCase: true);
        var activeDate = ActiveDate.ToLocalDateTime().Date;
        var inactiveDate = InactiveDate?.ToLocalDateTime().Date;

        return new ImportTeamDto(teamType, Name, (TeamCode)Code, Description, activeDate, IsActive, inactiveDate);
    }
}

public sealed class ImportTeamRequestValidator : CustomValidator<ImportTeamRequest>
{
    public ImportTeamRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<TeamType>(t.Trim(), ignoreCase: true, out _))
                .WithMessage("Type must be either 'Team' or 'TeamOfTeams'.");

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Code)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(10)
            .Must(t => t.IsValidTeamCodeFormat())
                .WithMessage("Invalid code format. Team codes are uppercase letters and numbers only, 2-10 characters.");

        RuleFor(t => t.Description)
            .MaximumLength(1024);

        RuleFor(t => t.ActiveDate)
            .NotEmpty();

        When(t => !t.IsActive, () =>
        {
            RuleFor(t => t.InactiveDate)
                .NotNull()
                    .WithMessage("An inactive team must have an InactiveDate.")
                .Must((t, inactiveDate) => inactiveDate > t.ActiveDate)
                    .WithMessage("The InactiveDate must be after the ActiveDate.");
        }).Otherwise(() =>
        {
            RuleFor(t => t.InactiveDate)
                .Null()
                    .WithMessage("An active team must not have an InactiveDate.");
        });
    }
}
