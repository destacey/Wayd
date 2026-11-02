using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.DemandEstimationScales;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Sets, changes or clears an intake request's estimate, on the estimation scale version its desk pins.
/// </summary>
/// <remarks>
/// The desk team's call: an agent or Desk Admin on the request's desk, or the Demand Management Admin, with no
/// claim beside it. Never the requester. Only while the request is open.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="Value">An estimate value of the desk's scale version, or null to clear the estimate.</param>
public sealed record ChangeIntakeRequestEstimateCommand(Guid Id, string? Value) : ICommand, IRequireLinkedEmployee;

public sealed class ChangeIntakeRequestEstimateCommandValidator : CustomValidator<ChangeIntakeRequestEstimateCommand>
{
    public ChangeIntakeRequestEstimateCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Value)
            .Must(v => v is null || !string.IsNullOrWhiteSpace(v))
                .WithMessage("Send an estimate value, or null to clear the estimate.")
            .Must(v => v is null || v.Trim().Length <= 32)
                .WithMessage("An estimate value can be at most 32 characters.");
    }
}

/// <summary>
/// Checks the caller is an agent on the request's desk, loads the scale the desk pins, and lets the request decide.
/// </summary>
public sealed class ChangeIntakeRequestEstimateCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeIntakeRequestEstimateCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeIntakeRequestEstimateCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        var desk = await _dbContext.IntakeDesks.AsNoTracking().FirstAsync(d => d.Id == intakeRequest.DeskId, cancellationToken);
        var scale = await _dbContext.FindPinnedScale(desk, cancellationToken);

        var editor = new IntakeRequestEditor(reader.EmployeeId, IsDeskAgent: true,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId));
        var result = intakeRequest.ChangeEstimate(request.Value, desk, scale, IntakeRequestEstimateSource.Manual, editor,
            _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
