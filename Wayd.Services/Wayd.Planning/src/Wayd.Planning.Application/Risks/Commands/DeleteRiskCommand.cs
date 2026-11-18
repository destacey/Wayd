using Wayd.Common.Application.Links;
using Wayd.Planning.Domain;

namespace Wayd.Planning.Application.Risks.Commands;

/// <summary>
/// Deletes a risk. The risk is soft deleted, so it drops out of every list and lookup but its activity history
/// is kept.
/// </summary>
public sealed record DeleteRiskCommand(Guid Id) : ICommand;

/// <summary>
/// Handles <see cref="DeleteRiskCommand"/>.
/// </summary>
public sealed class DeleteRiskCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteRiskCommandHandler> logger, ILinkWriter linkWriter) : ICommandHandler<DeleteRiskCommand>
{
    private readonly ILinkWriter _linkWriter = linkWriter;
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

        var actor = EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId());
        risk.Delete(actor, _dateTimeProvider.Now);

        _planningDbContext.Risks.Remove(risk);

        // A link has no foreign key to its record, and a deleted risk is reachable from nowhere to remove them later.
        await _linkWriter.RemoveAll(PlanningLinkOwners.Risk.Key, risk.Id, actor, cancellationToken);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Risk {RiskId} deleted.", risk.Id);

        return Result.Success();
    }
}
