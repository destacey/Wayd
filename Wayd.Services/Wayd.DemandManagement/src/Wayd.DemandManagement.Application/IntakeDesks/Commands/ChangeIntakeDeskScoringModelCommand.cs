using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Sets the scoring model a desk's requests are scored with, or stops scoring them.
/// </summary>
/// <remarks>
/// A Desk Admin on the desk or the Demand Management Admin, with no claim beside it: how a desk ranks its
/// requests is part of running it. Scores already recorded keep the model they froze.
/// </remarks>
/// <param name="Id">The desk.</param>
/// <param name="ScoringModelId">An active scoring model, or null to stop scoring.</param>
public sealed record ChangeIntakeDeskScoringModelCommand(Guid Id, Guid? ScoringModelId) : ICommand;

public sealed class ChangeIntakeDeskScoringModelCommandValidator : CustomValidator<ChangeIntakeDeskScoringModelCommand>
{
    public ChangeIntakeDeskScoringModelCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ScoringModelId).NotEqual(Guid.Empty);
    }
}

public sealed class ChangeIntakeDeskScoringModelCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeIntakeDeskScoringModelCommand>
{
    /// <summary>The failure for a scoring model that does not exist.</summary>
    public const string ScoringModelNotFoundError = "Scoring model not found.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeIntakeDeskScoringModelCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var desk = await _dbContext.IntakeDesks.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (desk is null)
            return Result.Failure(IntakeDeskAccess.NotFoundError);

        if (!reader.IsDeskAdminOn(desk.Id))
            return Result.Failure(IntakeDeskAccess.NotADeskAdminError);

        var model = request.ScoringModelId is { } modelId
            ? await _dbContext.ScoringModels.FirstOrDefaultAsync(m => m.Id == modelId, cancellationToken)
            : null;
        if (request.ScoringModelId is not null && model is null)
            return Result.Failure(ScoringModelNotFoundError);

        var current = desk.ScoringModelId is { } currentId
            ? await _dbContext.ScoringModels.FirstAsync(m => m.Id == currentId, cancellationToken)
            : null;

        var result = desk.ChangeScoringModel(model, current,
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
