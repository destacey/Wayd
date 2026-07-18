using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTypeLevels.Commands;

public sealed record CreateWorkTypeLevelCommand(string Name, string? Description) : ICommand<int>;

public sealed class CreateWorkTypeLevelCommandValidator : CustomValidator<CreateWorkTypeLevelCommand>
{
    private readonly IWorkDbContext _workDbContext;

    public CreateWorkTypeLevelCommandValidator(IWorkDbContext workDbContext)
    {
        _workDbContext = workDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(BeUniqueName).WithMessage("The work type level name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(1024);
    }

    public async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
    {
        var levelNames = await _workDbContext.WorkTypeHierarchies
            .SelectMany(s => s.Levels.Select(l => l.Name))
            .ToListAsync(cancellationToken);
        return levelNames.All(l => l != name.Trim());
    }
}

public sealed class CreateWorkTypeLevelCommandHandler : ICommandHandler<CreateWorkTypeLevelCommand, int>
{
    private const string AppRequestName = nameof(CreateWorkTypeLevelCommand);

    private readonly IWorkDbContext _workDbContext;
    private readonly Instant _timestamp;
    private readonly ILogger<CreateWorkTypeLevelCommandHandler> _logger;

    public CreateWorkTypeLevelCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateWorkTypeLevelCommandHandler> logger)
    {
        _workDbContext = workDbContext;
        _timestamp = dateTimeProvider.Now;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(CreateWorkTypeLevelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var hierarchy = await _workDbContext.WorkTypeHierarchies
                .FirstOrDefaultAsync(cancellationToken);

            if (hierarchy is null)
                return Result.Failure<int>("The system work type hierarchy does not exist.");

            var addResult = hierarchy.AddPortfolioWorkTypeLevel(request.Name, request.Description, _timestamp);
            if (addResult.IsFailure)
                return Result.Failure<int>(addResult.Error);

            await _workDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(addResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", AppRequestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {AppRequestName} {request}");
        }
    }
}

