using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.Web.Api.Models.Ppm.Projects;

public sealed record UpdateProjectRequest
{
    /// <summary>
    /// The unique identifier of the project.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the project.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// A concise summary of what the project delivers and its scope.
    /// Serves as the elevator pitch — what is being built or delivered.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// The strategic justification for the project — why it should be funded.
    /// Captures the problem being solved or opportunity being pursued and the strategic rationale.
    /// </summary>
    public string? BusinessCase { get; set; }

    /// <summary>
    /// The specific, measurable outcomes expected upon successful delivery.
    /// Examples: revenue growth, cost savings, compliance achievement, efficiency improvements.
    /// </summary>
    public string? ExpectedBenefits { get; set; }

    /// <summary>
    /// The ID of the expenditure category associated with the project.
    /// </summary>
    public int ExpenditureCategoryId { get; set; }

    /// <summary>
    /// The Project start date.
    /// </summary>
    public LocalDate? Start { get; set; }

    /// <summary>
    /// The Project end date.
    /// </summary>
    public LocalDate? End { get; set; }

    /// <summary>
    /// The sponsors of the project.
    /// </summary>
    public List<Guid>? SponsorIds { get; set; } = [];

    /// <summary>
    /// The owners of the project.
    /// </summary>
    public List<Guid>? OwnerIds { get; set; } = [];

    /// <summary>
    /// The managers of the project.
    /// </summary>
    public List<Guid>? ManagerIds { get; set; } = [];

    /// <summary>
    /// The members of the project team.
    /// </summary>
    public List<Guid>? MemberIds { get; set; } = [];

    /// <summary>
    /// The strategic themes associated with this project.
    /// </summary>
    public List<Guid>? StrategicThemeIds { get; set; } = [];

    public UpdateProjectCommand ToUpdateProjectCommand()
    {
        var dateRange = Start is null || End is null ? null : new LocalDateRange((LocalDate)Start, (LocalDate)End);

        return new UpdateProjectCommand(Id, Name, Description, BusinessCase, ExpectedBenefits, ExpenditureCategoryId, dateRange, SponsorIds, OwnerIds, ManagerIds, MemberIds, StrategicThemeIds);
    }

    /// <summary>
    /// The update that would leave <paramref name="dto"/> exactly as it is: the record a JSON Patch is applied to.
    /// </summary>
    public static UpdateProjectRequest FromDto(ProjectDetailsDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        BusinessCase = dto.BusinessCase,
        ExpectedBenefits = dto.ExpectedBenefits,
        ExpenditureCategoryId = dto.ExpenditureCategory.Id,
        Start = dto.Start,
        End = dto.End,
        SponsorIds = [.. dto.ProjectSponsors.Select(e => e.Id)],
        OwnerIds = [.. dto.ProjectOwners.Select(e => e.Id)],
        ManagerIds = [.. dto.ProjectManagers.Select(e => e.Id)],
        MemberIds = [.. dto.ProjectMembers.Select(e => e.Id)],
        StrategicThemeIds = [.. dto.StrategicThemes.Select(t => t.Id)],
    };
}

public sealed class UpdateProjectProjectRequestValidator : CustomValidator<UpdateProjectRequest>
{
    public UpdateProjectProjectRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(4096);

        RuleFor(p => p.BusinessCase)
            .MaximumLength(4096);

        RuleFor(p => p.ExpectedBenefits)
            .MaximumLength(4096);

        RuleFor(p => p.ExpenditureCategoryId)
            .GreaterThan(0);

        RuleFor(p => p)
            .Must(p => (p.Start == null && p.End == null) || (p.Start != null && p.End != null))
            .WithMessage("Start and End must either both be null or both have a value.");

        RuleFor(p => p)
            .Must(p => p.Start == null || p.End == null || p.Start <= p.End)
            .WithMessage("End date must be greater than or equal to start date.");

        RuleFor(x => x.SponsorIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("SponsorIds cannot contain empty GUIDs.");

        RuleFor(x => x.OwnerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("OwnerIds cannot contain empty GUIDs.");

        RuleFor(x => x.ManagerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("ManagerIds cannot contain empty GUIDs.");

        RuleFor(x => x.MemberIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("MemberIds cannot contain empty GUIDs.");

        RuleFor(p => p.StrategicThemeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("StrategicThemeIds cannot contain empty GUIDs.");
    }
}
