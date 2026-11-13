using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Pins a desk to the current version of an estimation scale, or stops estimating its requests, and decides what
/// happens to the estimates on its open requests.
/// </summary>
/// <remarks>
/// A Desk Admin on the desk or the Demand Management Admin, with no claim beside it, as for the scoring model.
/// </remarks>
/// <param name="Id">The desk.</param>
/// <param name="EstimationScaleId">An active estimation scale, or null to stop estimating.</param>
/// <param name="OpenEstimates">Whether open requests keep their estimates or have them cleared.</param>
public sealed record ChangeIntakeDeskEstimationScaleCommand(
    Guid Id,
    Guid? EstimationScaleId,
    ScaleChangeEstimateHandling OpenEstimates = ScaleChangeEstimateHandling.Keep) : ICommand;

public sealed class ChangeIntakeDeskEstimationScaleCommandValidator : CustomValidator<ChangeIntakeDeskEstimationScaleCommand>
{
    public ChangeIntakeDeskEstimationScaleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EstimationScaleId).NotEqual(Guid.Empty);
        RuleFor(c => c.OpenEstimates).IsInEnum();
    }
}

/// <summary>
/// Checks the caller is a Desk Admin, loads the desk's estimated open requests, and lets the desk decide.
/// </summary>
public sealed class ChangeIntakeDeskEstimationScaleCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeIntakeDeskEstimationScaleCommand>
{
    /// <summary>The failure for an estimation scale that does not exist, or that Demand Management has no copy of yet.</summary>
    public const string EstimationScaleNotFoundError = "Estimation scale not found.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeIntakeDeskEstimationScaleCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var desk = await _dbContext.IntakeDesks.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (desk is null)
            return Result.Failure(IntakeDeskAccess.NotFoundError);

        if (!reader.IsDeskAdminOn(desk.Id))
            return Result.Failure(IntakeDeskAccess.NotADeskAdminError);

        var scale = request.EstimationScaleId is { } scaleId
            ? await _dbContext.DemandEstimationScales.AsNoTracking().Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.Id == scaleId, cancellationToken)
            : null;
        if (request.EstimationScaleId is not null && scale is null)
            return Result.Failure(EstimationScaleNotFoundError);

        var current = desk.EstimationScaleVersionId is { } versionId
            ? await _dbContext.DemandEstimationScales.AsNoTracking().Include(s => s.Versions)
                .FirstAsync(s => s.Versions.Any(v => v.Id == versionId), cancellationToken)
            : null;

        var estimatedOpenRequests = await _dbContext.IntakeRequests
            .Where(r => r.DeskId == desk.Id && r.Estimate != null
                && (r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active))
            .ToListAsync(cancellationToken);

        var result = desk.ChangeEstimationScale(scale, current, estimatedOpenRequests, request.OpenEstimates,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(IntakeDeskAccess.StaleError);
        }

        return Result.Success();
    }
}
