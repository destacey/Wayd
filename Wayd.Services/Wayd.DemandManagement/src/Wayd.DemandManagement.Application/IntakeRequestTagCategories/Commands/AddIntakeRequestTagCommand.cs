using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Adds a tag to an axis. The Administer permission is checked by the endpoint.
/// </summary>
/// <param name="CategoryId">The axis.</param>
/// <param name="Name">The label, unique within the axis.</param>
/// <param name="Description">What the label means, where it is not obvious.</param>
public sealed record AddIntakeRequestTagCommand(Guid CategoryId, string Name, string? Description) : ICommand<Guid>;

public sealed class AddIntakeRequestTagCommandValidator : CustomValidator<AddIntakeRequestTagCommand>
{
    public AddIntakeRequestTagCommandValidator()
    {
        RuleFor(c => c.CategoryId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(512);
    }
}

public sealed class AddIntakeRequestTagCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AddIntakeRequestTagCommand, Guid>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(AddIntakeRequestTagCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.IntakeRequestTagCategories
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure<Guid>(IntakeRequestTagCategoryErrors.NotFoundError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = category.AddTag(request.Name, request.Description,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The name was checked above, so a failed save is another one taking it meanwhile.
            return Result.Failure<Guid>(IntakeRequestTagCategory.DuplicateTagNameError);
        }

        return result.Value.Id;
    }
}
