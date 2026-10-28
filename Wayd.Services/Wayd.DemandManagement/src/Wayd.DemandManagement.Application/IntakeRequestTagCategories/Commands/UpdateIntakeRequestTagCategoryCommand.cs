using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Renames a tag axis or changes its description. Whether it takes several tags is fixed at creation. The
/// Administer permission is checked by the endpoint.
/// </summary>
/// <param name="Id">The axis.</param>
/// <param name="Name">What the company calls the axis. Unique among active and inactive axes.</param>
/// <param name="Description">What the axis is for, or null for no description.</param>
public sealed record UpdateIntakeRequestTagCategoryCommand(Guid Id, string Name, string? Description) : ICommand;

public sealed class UpdateIntakeRequestTagCategoryCommandValidator : CustomValidator<UpdateIntakeRequestTagCategoryCommand>
{
    public UpdateIntakeRequestTagCategoryCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(512);
    }
}

public sealed class UpdateIntakeRequestTagCategoryCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateIntakeRequestTagCategoryCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateIntakeRequestTagCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.IntakeRequestTagCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (category is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.NotFoundError);

        var name = request.Name.Trim();
        if (await _dbContext.IntakeRequestTagCategories.AnyAsync(c => c.Id != category.Id && c.Name == name, cancellationToken))
            return Result.Failure(IntakeRequestTagCategoryErrors.DuplicateNameError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = category.UpdateDetails(name, request.Description,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The name was checked above, so a failed save is another one taking it meanwhile.
            return Result.Failure(IntakeRequestTagCategoryErrors.DuplicateNameError);
        }

        return Result.Success();
    }
}
