using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Takes a tag out of use, or puts it back. Requests already carrying it keep it and can still have it removed.
/// The Administer permission is checked by the endpoint.
/// </summary>
/// <param name="CategoryId">The tag's axis.</param>
/// <param name="TagId">The tag.</param>
/// <param name="IsActive">Whether requests can be given the tag.</param>
public sealed record SetIntakeRequestTagActiveCommand(Guid CategoryId, Guid TagId, bool IsActive) : ICommand;

public sealed class SetIntakeRequestTagActiveCommandValidator : CustomValidator<SetIntakeRequestTagActiveCommand>
{
    public SetIntakeRequestTagActiveCommandValidator()
    {
        RuleFor(c => c.CategoryId).NotEmpty();
        RuleFor(c => c.TagId).NotEmpty();
    }
}

public sealed class SetIntakeRequestTagActiveCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetIntakeRequestTagActiveCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(SetIntakeRequestTagActiveCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.IntakeRequestTagCategories
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.NotFoundError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
        var result = request.IsActive
            ? category.ActivateTag(request.TagId, actor, _dateTimeProvider.Now)
            : category.DeactivateTag(request.TagId, actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
