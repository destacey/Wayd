using Wayd.Common.Extensions;
using Wayd.Organization.Application.Teams.Dtos;

namespace Wayd.Web.Api.Models.Organizations.Teams;

/// <summary>
/// A single CSV row for the team-hierarchy import: places a child team (or team of teams) under a parent
/// team of teams for a date range, all by natural key.
/// </summary>
public sealed class ImportTeamMembershipRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The child team's code. May be a Team or a Team of Teams.</summary>
    public string ChildCode { get; set; } = default!;

    /// <summary>The parent's code. Must be a Team of Teams, and cannot equal ChildCode.</summary>
    public string ParentCode { get; set; } = default!;

    /// <summary>When the membership begins. On or after both teams' active dates.</summary>
    public DateOnly Start { get; set; }

    /// <summary>When the membership ends. On or after Start; blank for one still in place.</summary>
    public DateOnly? End { get; set; }

    public ImportTeamMembershipDto ToImportTeamMembershipDto()
    {
        var start = Start.ToLocalDate();
        var end = End?.ToLocalDate();

        return new ImportTeamMembershipDto(ChildCode.Trim(), ParentCode.Trim(), start, end);
    }
}

public sealed class ImportTeamMembershipRequestValidator : CustomValidator<ImportTeamMembershipRequest>
{
    public ImportTeamMembershipRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.ChildCode)
            .NotEmpty()
            .MaximumLength(10);

        RuleFor(m => m.ParentCode)
            .NotEmpty()
            .MaximumLength(10)
            .NotEqual(m => m.ChildCode)
                .WithMessage("A team cannot be its own parent.");

        RuleFor(m => m.Start)
            .NotEmpty();

        RuleFor(m => m.End)
            .Must((m, end) => end is null || m.Start <= end)
                .WithMessage("End date must be on or after the start date.");
    }
}
