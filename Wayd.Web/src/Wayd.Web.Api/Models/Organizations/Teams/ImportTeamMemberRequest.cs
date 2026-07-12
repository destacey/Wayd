using Wayd.Organization.Application.Teams.Dtos;

namespace Wayd.Web.Api.Models.Organizations.Teams;

/// <summary>
/// A single CSV row for team staffing: places one employee on one team in one role, all by natural key.
/// Multiple roles for the same employee on the same team are expressed as multiple rows.
/// </summary>
public sealed class ImportTeamMemberRequest
{
    public string TeamCode { get; set; } = default!;
    public string EmployeeNumber { get; set; } = default!;
    public string RoleName { get; set; } = default!;

    public ImportTeamMemberDto ToImportTeamMemberDto()
    {
        return new ImportTeamMemberDto(TeamCode.Trim(), EmployeeNumber.Trim(), RoleName.Trim());
    }
}

public sealed class ImportTeamMemberRequestValidator : CustomValidator<ImportTeamMemberRequest>
{
    public ImportTeamMemberRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.TeamCode)
            .NotEmpty()
            .MaximumLength(10);

        RuleFor(m => m.EmployeeNumber)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(m => m.RoleName)
            .NotEmpty()
            .MaximumLength(128);
    }
}
