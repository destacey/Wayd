using Ardalis.GuardClauses;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record ManagePlanningIntervalDatesCommand : ICommand
{
    public ManagePlanningIntervalDatesCommand(Guid id, LocalDateRange dateRange, IEnumerable<PlanningIntervalIterationUpsertDto> iterations)
    {
        Id = id;
        DateRange = Guard.Against.Null(dateRange);
        Iterations = iterations?.ToList() ?? [];
    }

    public Guid Id { get; }
    public LocalDateRange DateRange { get; }
    public List<PlanningIntervalIterationUpsertDto> Iterations { get; } = [];
}

public sealed class ManagePlanningIntervalDatesCommandValidator : CustomValidator<ManagePlanningIntervalDatesCommand>
{
    public ManagePlanningIntervalDatesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.DateRange)
            .NotNull();

        RuleFor(c => c.Iterations)
            .NotNull()
            .ForEach(x => x.SetValidator(new PlanningIntervalIterationUpsertDtoValidator()));
    }
}


public sealed class ManagePlanningIntervalDatesCommandHandler(IPlanningDbContext planningDbContext, ILogger<ManagePlanningIntervalDatesCommandHandler> logger) : ICommandHandler<ManagePlanningIntervalDatesCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<ManagePlanningIntervalDatesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ManagePlanningIntervalDatesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var planningInterval = await _planningDbContext.PlanningIntervals
                .Include(x => x.Iterations)
                .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (planningInterval is null)
            {
                _logger.LogWarning("Planning Interval with Id {PlanningIntervalId} not found.", request.Id);
                return Result.Failure($"Planning Interval with Id {request.Id} not found.");
            }

            var iterations = request.Iterations
                .Select(i => UpsertPlanningIntervalIteration.Create(i.IterationId, i.Name, i.Category, i.DateRange)).ToList();

            var result = planningInterval.ManageDates(request.DateRange, iterations);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {CommandName} command.", nameof(ManagePlanningIntervalTeamsCommand));
            return Result.Failure($"Error handling {nameof(ManagePlanningIntervalTeamsCommand)} command.");
        }
    }
}
