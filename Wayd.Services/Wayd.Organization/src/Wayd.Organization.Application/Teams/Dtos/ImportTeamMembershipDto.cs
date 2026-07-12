using NodaTime;

namespace Wayd.Organization.Application.Teams.Dtos;

/// <summary>
/// A single team-hierarchy row: places a child (a Team or a Team of Teams) under a parent Team of Teams for
/// a date range. Everything is referenced by natural key — child by <see cref="ChildCode"/>, parent by
/// <see cref="ParentCode"/> — so the batch can be authored without knowing generated Ids. The parent must be
/// a Team of Teams; the child may be a Team (leaf) or a Team of Teams (an intermediate tier), which lets a
/// three-tier value-stream / ART / team hierarchy be imported.
/// </summary>
public sealed record ImportTeamMembershipDto(
    string ChildCode,
    string ParentCode,
    LocalDate Start,
    LocalDate? End);

public sealed class ImportTeamMembershipDtoValidator : CustomValidator<ImportTeamMembershipDto>
{
    public ImportTeamMembershipDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.ChildCode)
            .NotEmpty();

        RuleFor(m => m.ParentCode)
            .NotEmpty()
            .NotEqual(m => m.ChildCode)
                .WithMessage("A team cannot be its own parent.");

        RuleFor(m => m.Start)
            .NotEmpty();

        RuleFor(m => m.End)
            .Must((m, end) => end is null || m.Start <= end)
                .WithMessage("End date must be on or after the start date.");
    }
}
