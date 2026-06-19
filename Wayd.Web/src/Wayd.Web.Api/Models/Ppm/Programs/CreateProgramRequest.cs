using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

namespace Wayd.Web.Api.Models.Ppm.Programs;

public sealed record CreateProgramRequest
{
    /// <summary>
    /// The name of the program.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// A detailed description of the program's purpose.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// The Program start date.
    /// </summary>
    public LocalDate? Start { get; set; }

    /// <summary>
    /// The Program end date.
    /// </summary>
    public LocalDate? End { get; set; }

    /// <summary>
    /// The ID of the portfolio to which this program belongs.
    /// </summary>
    public Guid PortfolioId { get; set; }

    /// <summary>
    /// The sponsors of the program.
    /// </summary>
    public List<Guid>? SponsorIds { get; set; } = [];

    /// <summary>
    /// The owners of the program.
    /// </summary>
    public List<Guid>? OwnerIds { get; set; } = [];

    /// <summary>
    /// The managers of the program.
    /// </summary>
    public List<Guid>? ManagerIds { get; set; } = [];

    /// <summary>
    /// The strategic themes associated with this program.
    /// </summary>
    public List<Guid>? StrategicThemeIds { get; set; } = [];

    public CreateProgramCommand ToCreateProgramCommand()
    {
        var dateRange = Start is null || End is null ? null : new LocalDateRange((LocalDate)Start, (LocalDate)End);

        return new CreateProgramCommand(Name, Description, dateRange, PortfolioId, SponsorIds, OwnerIds, ManagerIds, StrategicThemeIds);
    }
}

public sealed class CreateProgramRequestValidator : CustomValidator<CreateProgramRequest>
{
    public CreateProgramRequestValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(p => p)
            .Must(p => (p.Start == null && p.End == null) || (p.Start != null && p.End != null))
            .WithMessage("Start and End must either both be null or both have a value.");

        RuleFor(p => p)
            .Must(p => p.Start == null || p.End == null || p.Start <= p.End)
            .WithMessage("End date must be greater than or equal to start date.");

        RuleFor(p => p.PortfolioId)
            .NotEmpty();

        RuleFor(x => x.SponsorIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("SponsorIds cannot contain empty GUIDs.");

        RuleFor(x => x.OwnerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("OwnerIds cannot contain empty GUIDs.");

        RuleFor(x => x.ManagerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("ManagerIds cannot contain empty GUIDs.");

        RuleFor(p => p.StrategicThemeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("StrategicThemeIds cannot contain empty GUIDs.");
    }
}
