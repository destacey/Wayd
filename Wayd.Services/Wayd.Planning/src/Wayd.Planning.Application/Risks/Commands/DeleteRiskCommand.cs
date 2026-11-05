namespace Wayd.Planning.Application.Risks.Commands;

/// <summary>
/// Deletes a risk. The risk is soft deleted, so it drops out of every list and lookup but its activity history
/// is kept.
/// </summary>
public sealed record DeleteRiskCommand(Guid Id) : ICommand;

/// <summary>
/// Handles <see cref="DeleteRiskCommand"/>.
/// </summary>
public sealed class DeleteRiskCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteRiskCommandHandler> logger) : ICommandHandler<DeleteRiskCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeleteRiskCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteRiskCommand request, CancellationToken cancellationToken)
    {
        var risk = await _planningDbContext.Risks
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (risk is null)
            return Result.Failure("Risk not found.");

        risk.Delete(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);

        _planningDbContext.Risks.Remove(risk);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Risk {RiskId} deleted.", risk.Id);

        return Result.Success();
    }
}
