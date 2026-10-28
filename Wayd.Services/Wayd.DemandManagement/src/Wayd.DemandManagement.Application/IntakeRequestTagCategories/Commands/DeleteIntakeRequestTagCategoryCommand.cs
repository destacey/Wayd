using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Deletes a tag axis no request is tagged along, with its tags. An axis in use is deactivated instead. The
/// Administer permission is checked by the endpoint.
/// </summary>
/// <remarks>
/// Each desk that chose the axis has it taken off through the desk, so the desk records the change and moves to
/// a new revision; a desk left with no axis chosen then tags along none.
/// </remarks>
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
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
        var now = _dateTimeProvider.Now;
        var result = category.Delete(isInUse, actor, now);
        if (result.IsFailure)
            return result;

        var desks = await _dbContext.IntakeDesks
            .Include(d => d.TagCategories)
            .Where(d => d.TagCategories.Any(c => c.CategoryId == category.Id))
            .ToListAsync(cancellationToken);
        foreach (var desk in desks)
        {
            desk.ChangeTagCategories(false,
                [.. desk.TagCategories.Select(c => c.CategoryId).Where(id => id != category.Id)], actor, now);
        }

        _dbContext.IntakeRequestTagCategories.Remove(category);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The checks above passed, so a failed save is a request tagged along it, or a desk choosing it, since.
            return Result.Failure(IntakeRequestTagCategoryErrors.ChangedWhileSavingError);
        }

        return Result.Success();
    }
}
