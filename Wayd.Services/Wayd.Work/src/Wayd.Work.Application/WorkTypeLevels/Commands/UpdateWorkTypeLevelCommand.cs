using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTypeLevels.Commands;

public sealed record UpdateWorkTypeLevelCommand : ICommand
{
    public UpdateWorkTypeLevelCommand(int id, string name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public int Id { get; }

    /// <summary>The name of the work type.  The name cannot be changed.</summary>
    /// <value>The name.</value>
    public string Name { get; }

    /// <summary>The description of the work type.</summary>
    /// <value>The description.</value>
    public string? Description { get; }
}

public sealed class UpdateWorkTypeLevelCommandValidator : CustomValidator<UpdateWorkTypeLevelCommand>
{
    private readonly IWorkDbContext _workDbContext;

    public UpdateWorkTypeLevelCommandValidator(IWorkDbContext workDbContext)
    {
        _workDbContext = workDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(async (cmd, workTypeLevel, cancellationToken) => await BeUniqueName(cmd.Id, workTypeLevel, cancellationToken)).WithMessage("The work type level name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(1024);
    }

    public async Task<bool> BeUniqueName(int id, string name, CancellationToken cancellationToken)
    {
        var levelNames = await _workDbContext.WorkTypeHierarchies
            .SelectMany(s => s.Levels.Where(c => c.Id != id).Select(l => l.Name))
            .ToListAsync(cancellationToken);

        return levelNames.All(l => l != name);
    }
}

public sealed class UpdateWorkTypeLevelCommandHandler : ICommandHandler<UpdateWorkTypeLevelCommand>
{
    private const string AppRequestName = nameof(UpdateWorkTypeLevelCommand);

    private readonly IWorkDbContext _workDbContext;
    private readonly Instant _timestamp;
    private readonly ILogger<UpdateWorkTypeLevelCommandHandler> _logger;

    public UpdateWorkTypeLevelCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateWorkTypeLevelCommandHandler> logger)
    {
        _workDbContext = workDbContext;
        _timestamp = dateTimeProvider.Now;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateWorkTypeLevelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var hierarchy = await _workDbContext.WorkTypeHierarchies
                .FirstOrDefaultAsync(cancellationToken);

            if (hierarchy is null)
                return Result.Failure("The system work type hierarchy does not exist.");

            var updateResult = hierarchy.UpdateWorkTypeLevel(request.Id, request.Name, request.Description, _timestamp);

            if (updateResult.IsFailure)
            {
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", AppRequestName, request, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", AppRequestName, request);

            return Result.Failure($"Wayd Request: Exception for Request {AppRequestName} {request}");
        }
    }
}
