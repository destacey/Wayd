using Wayd.Common.Application.Requests.Goals.Commands;
using Wayd.Goals.Application.Persistence;

namespace Wayd.Goals.Application.Objectives.Commands;

public sealed class DeleteObjectiveCommandHandler(IGoalsDbContext goalsDbContext, ILogger<DeleteObjectiveCommandHandler> logger) : ICommandHandler<DeleteObjectiveCommand>
{
    private readonly IGoalsDbContext _goalsDbContext = goalsDbContext;
    private readonly ILogger<DeleteObjectiveCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteObjectiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var objective = await _goalsDbContext.Objectives.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
            if (objective is null)
                return Result.Failure($"Objective with id {request.Id} not found.");

            _goalsDbContext.Objectives.Remove(objective);

            await _goalsDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}