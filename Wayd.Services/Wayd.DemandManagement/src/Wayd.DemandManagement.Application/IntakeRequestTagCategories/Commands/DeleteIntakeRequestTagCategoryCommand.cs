using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Deletes a tag axis no request is tagged along, with its tags. An axis in use is deactivated instead. The
/// Administer permission is checked by the endpoint.
/// </summary>
/// <param name="Id">The axis.</param>
public sealed record DeleteIntakeRequestTagCategoryCommand(Guid Id) : ICommand;

public sealed class DeleteIntakeRequestTagCategoryCommandValidator : CustomValidator<DeleteIntakeRequestTagCategoryCommand>
{
    public DeleteIntakeRequestTagCategoryCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteIntakeRequestTagCategoryCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteIntakeRequestTagCategoryCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteIntakeRequestTagCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.IntakeRequestTagCategories
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (category is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.NotFoundError);

        // Every request, visible to the caller or not: an axis is in use whoever can see the requests filed on it.
        var isInUse = await _dbContext.IntakeRequests
            .AnyAsync(r => r.Tags.Any(a => a.CategoryId == category.Id), cancellationToken);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = category.Delete(isInUse, EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        _dbContext.IntakeRequestTagCategories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
