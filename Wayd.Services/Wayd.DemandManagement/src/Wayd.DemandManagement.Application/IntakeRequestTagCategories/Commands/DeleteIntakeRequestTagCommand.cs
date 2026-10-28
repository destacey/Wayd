using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Deletes a tag no request carries. A tag in use is deactivated instead. The Administer permission is checked
/// by the endpoint.
/// </summary>
/// <param name="CategoryId">The tag's axis.</param>
/// <param name="TagId">The tag.</param>
public sealed record DeleteIntakeRequestTagCommand(Guid CategoryId, Guid TagId) : ICommand;

public sealed class DeleteIntakeRequestTagCommandValidator : CustomValidator<DeleteIntakeRequestTagCommand>
{
    public DeleteIntakeRequestTagCommandValidator()
    {
        RuleFor(c => c.CategoryId).NotEmpty();
        RuleFor(c => c.TagId).NotEmpty();
    }
}

public sealed class DeleteIntakeRequestTagCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteIntakeRequestTagCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteIntakeRequestTagCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.IntakeRequestTagCategories
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.NotFoundError);

        // Every request, visible to the caller or not: a tag is in use whoever can see the requests carrying it.
        var isInUse = await _dbContext.IntakeRequests
            .AnyAsync(r => r.Tags.Any(a => a.TagId == request.TagId), cancellationToken);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = category.RemoveTag(request.TagId, isInUse,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The check above passed, so a failed save is a request given the tag since.
            return Result.Failure(IntakeRequestTagCategory.TagInUseError);
        }

        return Result.Success();
    }
}
