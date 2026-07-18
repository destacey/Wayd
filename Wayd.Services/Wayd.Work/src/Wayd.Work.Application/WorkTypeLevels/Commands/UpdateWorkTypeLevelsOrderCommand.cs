using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTypeLevels.Commands;

public sealed record UpdateWorkTypeLevelsOrderCommand(Dictionary<int, int> Levels) : ICommand;

public sealed class UpdateWorkTypeLevelsOrderCommandHandler : ICommandHandler<UpdateWorkTypeLevelsOrderCommand>
{
    private const string AppRequestName = nameof(UpdateWorkTypeLevelsOrderCommand);

    private readonly IWorkDbContext _workDbContext;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<UpdateWorkTypeLevelsOrderCommandHandler> _logger;

    public UpdateWorkTypeLevelsOrderCommandHandler(IWorkDbContext planningDbContext, IDispatcher dispatcher, ILogger<UpdateWorkTypeLevelsOrderCommandHandler> logger)
    {
        _workDbContext = planningDbContext;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateWorkTypeLevelsOrderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var hierarchy = await _workDbContext.WorkTypeHierarchies
                .FirstOrDefaultAsync(cancellationToken);

            if (hierarchy is null)
                return Result.Failure("The system work type hierarchy does not exist.");

            var updateResult = hierarchy.UpdatePortfolioTierLevelsOrder(request.Levels);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Failure handling {CommandName} command for request {@Request}. Error message: {Error}", AppRequestName, request, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }


}
