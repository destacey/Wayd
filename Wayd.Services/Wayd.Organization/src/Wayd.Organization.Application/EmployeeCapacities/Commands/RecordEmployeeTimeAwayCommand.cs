using NodaTime;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Organization.Application.EmployeeCapacities.Commands;

/// <summary>
/// Records an employee as away from every team they are allocated to from <paramref name="Start"/> to
/// <paramref name="End"/>, returning the id of the time away. Allowed for the employee, anyone they report up
/// to, and a time away administrator.
/// </summary>
/// <param name="PercentAway">The share of each day the employee is away, from 1 to 100.</param>
public sealed record RecordEmployeeTimeAwayCommand(Guid EmployeeId, LocalDate Start, LocalDate End, int PercentAway, TimeAwayReason Reason)
    : ICommand<Guid>, IRequireLinkedEmployee;

public sealed class RecordEmployeeTimeAwayCommandValidator : CustomValidator<RecordEmployeeTimeAwayCommand>
{
    public RecordEmployeeTimeAwayCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.EmployeeId)
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

public sealed class RecordEmployeeTimeAwayCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RecordEmployeeTimeAwayCommand, Guid>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(RecordEmployeeTimeAwayCommand request, CancellationToken cancellationToken)
    {
        // Before anything is read about the employee, so a caller cannot learn which ids exist.
        if (!await _currentPrincipal.CanManageTimeAway(_organizationDbContext, request.EmployeeId, cancellationToken))
            return Result.Failure<Guid>(TimeAwayAuthorization.NotAllowedError);

        if (!await _organizationDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
            return Result.Failure<Guid>($"Employee {request.EmployeeId} not found.");

        var (capacity, isNew) = await EmployeeCapacityRecords.LoadOrCreate(_organizationDbContext, request.EmployeeId, CapacityPart.TimeAway, cancellationToken);

        var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
        var result = capacity.RecordTimeAway(request.Start, request.End, request.PercentAway, request.Reason, actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        var saved = await EmployeeCapacityRecords.Save(_organizationDbContext, capacity, isNew, cancellationToken);

        return saved.IsSuccess
            ? result.Value.Id
            : Result.Failure<Guid>(saved.Error);
    }
}
