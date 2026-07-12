using Wayd.Organization.Application.Teams.Dtos;
using NodaTime.Extensions;

namespace Wayd.Web.Api.Models.Organizations.Teams;

/// <summary>
/// A single CSV row for the team-hierarchy import: places a child team (or team of teams) under a parent
/// team of teams for a date range, all by natural key.
/// </summary>
public sealed class ImportTeamMembershipRequest
{
    public string ChildCode { get; set; } = default!;
    public string ParentCode { get; set; } = default!;
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }

    public ImportTeamMembershipDto ToImportTeamMembershipDto()
    {
        var start = Start.ToLocalDateTime().Date;
        var end = End?.ToLocalDateTime().Date;

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
