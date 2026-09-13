using Wayd.Organization.Application.Teams.Dtos;

namespace Wayd.Web.Api.Models.Organizations.Teams;

/// <summary>
/// A single CSV row for team staffing: places one employee on one team in one role, all by natural key.
/// Multiple roles for the same employee on the same team are expressed as multiple rows.
/// </summary>
public sealed class ImportTeamMemberRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The team, by code.</summary>
    public string TeamCode { get; set; } = default!;

    /// <summary>The employee, by number. Must be an active employee.</summary>
    public string EmployeeNumber { get; set; } = default!;

    /// <summary>A team member role, by name. The role must already exist.</summary>
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
