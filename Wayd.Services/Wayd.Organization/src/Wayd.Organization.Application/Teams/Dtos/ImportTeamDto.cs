using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Dtos;

/// <summary>
/// A single team row to be imported. Teams and Teams of Teams share an identical create shape, so they
/// are imported through one unified command and discriminated by <see cref="Type"/>. Hierarchy between
/// teams is not expressed here — parent/child memberships are imported separately.
/// <para>
/// A row may represent an already-retired team by setting <see cref="IsActive"/> to <c>false</c> and
/// supplying an <see cref="InactiveDate"/> (useful for migrations and historical test fixtures). The
/// team is still created active and then deactivated through the domain, so the lifecycle events fire.
/// </para>
/// </summary>
public sealed record ImportTeamDto(
    TeamType Type,
    string Name,
    TeamCode Code,
    string? Description,
    LocalDate ActiveDate,
    bool IsActive = true,
    LocalDate? InactiveDate = null);

public sealed class ImportTeamDtoValidator : CustomValidator<ImportTeamDto>
{
    public ImportTeamDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Type)
            .IsInEnum();

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Code)
            .NotNull();

        RuleFor(t => t.Description)
            .MaximumLength(1024);

        RuleFor(t => t.ActiveDate)
            .NotEmpty();

        // An inactive team must carry an inactive date, and (matching the domain's Deactivate rule) that
        // date must be after the active date. An active team must not carry an inactive date.
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
