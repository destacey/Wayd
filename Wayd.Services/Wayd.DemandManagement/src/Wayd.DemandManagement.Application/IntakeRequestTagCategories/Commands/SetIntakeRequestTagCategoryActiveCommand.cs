using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Takes a tag category out of use, or puts it back. Requests already tagged in it keep their tags. Allowed on
/// a system category. The Administer permission is checked by the endpoint.
/// </summary>
/// <param name="Id">The category.</param>
/// <param name="IsActive">Whether requests can be tagged along it.</param>
public sealed record SetIntakeRequestTagCategoryActiveCommand(Guid Id, bool IsActive) : ICommand;

public sealed class SetIntakeRequestTagCategoryActiveCommandValidator : CustomValidator<SetIntakeRequestTagCategoryActiveCommand>
{
    public SetIntakeRequestTagCategoryActiveCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class SetIntakeRequestTagCategoryActiveCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetIntakeRequestTagCategoryActiveCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SetIntakeRequestTagCategoryActiveCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.IntakeRequestTagCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (category is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.NotFoundError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
        var result = request.IsActive
            ? category.Activate(actor, _dateTimeProvider.Now)
            : category.Deactivate(actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
