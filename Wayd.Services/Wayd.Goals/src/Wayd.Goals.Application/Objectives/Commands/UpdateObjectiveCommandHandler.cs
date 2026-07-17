using Wayd.Common.Application.Requests.Goals.Commands;
using Wayd.Goals.Application.Persistence;

namespace Wayd.Goals.Application.Objectives.Commands;

public sealed class UpdateObjectiveCommandHandler(IGoalsDbContext goalsDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateObjectiveCommandHandler> logger) : ICommandHandler<UpdateObjectiveCommand, Guid>
{
    private readonly IGoalsDbContext _goalsDbContext = goalsDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateObjectiveCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(UpdateObjectiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var objective = await _goalsDbContext.Objectives
                .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
            if (objective is null)
                return Result.Failure<Guid>("Objective not found.");

            var updateResult = objective.Update(
                request.Name,
                request.Description,
                request.Status,
                request.Progress,
                request.OwnerId,
                request.StartDate,
                request.TargetDate,
                _dateTimeProvider.Now
                );

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _goalsDbContext.Entry(objective).ReloadAsync(cancellationToken);
                objective.ClearDomainEvents();

                var requestName = request.GetType().Name;
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", requestName, request, updateResult.Error);
                return Result.Failure<Guid>(updateResult.Error);
            }

            await _goalsDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(objective.Id);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<Guid>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
