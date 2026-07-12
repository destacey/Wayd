namespace Wayd.Organization.Application.Teams.Dtos;

/// <summary>
/// A single team-staffing row: places one employee on one team in one role. Everything is referenced by
/// natural key — team by <see cref="TeamCode"/>, employee by <see cref="EmployeeNumber"/>, role by
/// <see cref="RoleName"/> — so the batch can be authored without knowing generated Ids. Multiple roles for
/// the same employee on the same team are expressed as multiple rows; the handler groups them so each
/// employee is added to each team once with all of their roles.
/// </summary>
public sealed record ImportTeamMemberDto(
    string TeamCode,
    string EmployeeNumber,
    string RoleName);

public sealed class ImportTeamMemberDtoValidator : CustomValidator<ImportTeamMemberDto>
{
    public ImportTeamMemberDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.TeamCode)
            .NotEmpty();

        RuleFor(m => m.EmployeeNumber)
            .NotEmpty();

        RuleFor(m => m.RoleName)
            .NotEmpty()
            .MaximumLength(128);
    }
}
