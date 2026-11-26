using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

namespace Wayd.Web.Api.Models.Ppm.Projects;

public sealed record CreateProjectRequest
{
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
    /// The unique key for the project (2-20 uppercase alphanumeric characters).
    /// </summary>
    public string Key { get; set; } = default!;

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
    /// The ID of the portfolio to which this project belongs.
    /// </summary>
    public Guid PortfolioId { get; set; }

    /// <summary>
    /// The ID of the program to which this project belongs (optional).
    /// </summary>
    public Guid? ProgramId { get; set; }

    /// <summary>
    /// The ID of the project lifecycle to assign (optional).
    /// </summary>
    public Guid? ProjectLifecycleId { get; set; }

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
    /// The strategic themes to tag the project with. Each must be Active; StrategicThemes_GetStrategicThemeOptions lists them.
    /// </summary>
    public List<Guid>? StrategicThemeIds { get; set; } = [];

    public CreateProjectCommand ToCreateProjectCommand()
    {
        var dateRange = Start is null || End is null ? null : new LocalDateRange((LocalDate)Start, (LocalDate)End);

        return new CreateProjectCommand(Name, Description, BusinessCase, ExpectedBenefits, new ProjectKey(Key), ExpenditureCategoryId, dateRange, PortfolioId, ProgramId, ProjectLifecycleId, SponsorIds, OwnerIds, ManagerIds, MemberIds, StrategicThemeIds);
    }
}

public sealed class CreateProjectRequestValidator : CustomValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
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

        RuleFor(p => p.Key)
            .NotNull()
            .Matches(ProjectKey.Regex)
                .WithMessage("Invalid code format. Project keys are uppercase letters and numbers only, 2-20 characters.");

        RuleFor(p => p.ExpenditureCategoryId)
            .GreaterThan(0);

        RuleFor(p => p)
            .Must(p => (p.Start == null && p.End == null) || (p.Start != null && p.End != null))
            .WithMessage("Start and End must either both be null or both have a value.");

        RuleFor(p => p)
            .Must(p => p.Start == null || p.End == null || p.Start <= p.End)
            .WithMessage("End date must be greater than or equal to start date.");

        RuleFor(p => p.PortfolioId)
            .NotEmpty();

        RuleFor(p => p.ProgramId)
            .Must(id => id == null || id != Guid.Empty)
            .WithMessage("ProgramId cannot be an empty GUID.");

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
