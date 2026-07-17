using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTypes.Commands;

public sealed record UpdateWorkTypeCommand(int Id, string? Description, int LevelId) : ICommand<int>;

public sealed class UpdateWorkTypeCommandValidator : CustomValidator<UpdateWorkTypeCommand>
{
    public UpdateWorkTypeCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.LevelId)
            .GreaterThan(0);
    }
}

public sealed class UpdateWorkTypeCommandHandler : ICommandHandler<UpdateWorkTypeCommand, int>
{
    private const string AppRequestName = nameof(UpdateWorkTypeCommand);

    private readonly IWorkDbContext _workDbContext;
    private readonly Instant _timestamp;
    private readonly ILogger<UpdateWorkTypeCommandHandler> _logger;

    public UpdateWorkTypeCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateWorkTypeCommandHandler> logger)
    {
        _workDbContext = workDbContext;
        _timestamp = dateTimeProvider.Now;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(UpdateWorkTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var workType = await _workDbContext.WorkTypes
                .FirstAsync(p => p.Id == request.Id, cancellationToken);
            if (workType is null)
                return Result.Failure<int>("Work Type not found.");

            var updateResult = workType.Update(request.Description, request.LevelId, _timestamp);

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _workDbContext.Entry(workType).ReloadAsync(cancellationToken);
                workType.ClearDomainEvents();

                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", AppRequestName, request, updateResult.Error);
                return Result.Failure<int>(updateResult.Error);
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(workType.Id);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}

