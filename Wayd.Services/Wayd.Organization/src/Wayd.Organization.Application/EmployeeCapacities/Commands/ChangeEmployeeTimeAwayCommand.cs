using NodaTime;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Organization.Application.EmployeeCapacities.Commands;

/// <summary>
/// Changes the days of a stretch of an employee's time away, how much of each day it takes, or its reason.
/// Allowed for the employee, anyone they report up to, and a time away administrator.
/// </summary>
/// <param name="PercentAway">The share of each day the employee is away, from 1 to 100.</param>
public sealed record ChangeEmployeeTimeAwayCommand(Guid EmployeeId, Guid TimeAwayId, LocalDate Start, LocalDate End, int PercentAway, TimeAwayReason Reason)
    : ICommand, IRequireLinkedEmployee;

public sealed class ChangeEmployeeTimeAwayCommandValidator : CustomValidator<ChangeEmployeeTimeAwayCommand>
{
    public ChangeEmployeeTimeAwayCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.EmployeeId)
            .NotEmpty();

        RuleFor(c => c.TimeAwayId)
            .NotEmpty();

        RuleFor(c => c.End)
            .GreaterThanOrEqualTo(c => c.Start)
            .WithMessage("Time away cannot end before it starts.");

        RuleFor(c => c.PercentAway)
            .InclusiveBetween(1, EmployeeTimeAway.FullDay);

        RuleFor(c => c.Reason)
            .IsInEnum();
    }
}

public sealed class ChangeEmployeeTimeAwayCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeEmployeeTimeAwayCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeEmployeeTimeAwayCommand request, CancellationToken cancellationToken)
    {
        // Before anything is read about the employee, so a caller cannot learn which ids exist.
        if (!await _currentPrincipal.CanManageTimeAway(_organizationDbContext, request.EmployeeId, cancellationToken))
            return Result.Failure(TimeAwayAuthorization.NotAllowedError);

        if (!await _organizationDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
            return Result.Failure($"Employee {request.EmployeeId} not found.");

        var (capacity, isNew) = await EmployeeCapacityRecords.LoadOrCreate(_organizationDbContext, request.EmployeeId, CapacityPart.TimeAway, cancellationToken);

        var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
        var result = capacity.ChangeTimeAway(request.TimeAwayId, request.Start, request.End, request.PercentAway, request.Reason, actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        return await EmployeeCapacityRecords.Save(_organizationDbContext, capacity, isNew, cancellationToken);
    }
}
