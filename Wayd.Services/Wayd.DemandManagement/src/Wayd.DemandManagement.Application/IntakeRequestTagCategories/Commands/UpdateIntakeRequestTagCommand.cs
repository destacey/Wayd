using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Renames a tag or changes its description. Every request carrying it reads the new name. The Administer
/// permission is checked by the endpoint.
/// </summary>
/// <param name="CategoryId">The tag's axis.</param>
/// <param name="TagId">The tag.</param>
/// <param name="Name">The label, unique within the axis.</param>
/// <param name="Description">What the label means, or null for no description.</param>
public sealed record UpdateIntakeRequestTagCommand(Guid CategoryId, Guid TagId, string Name, string? Description) : ICommand;

public sealed class UpdateIntakeRequestTagCommandValidator : CustomValidator<UpdateIntakeRequestTagCommand>
{
    public UpdateIntakeRequestTagCommandValidator()
    {
        RuleFor(c => c.CategoryId).NotEmpty();
        RuleFor(c => c.TagId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(512);
    }
}

public sealed class UpdateIntakeRequestTagCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateIntakeRequestTagCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateIntakeRequestTagCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.IntakeRequestTagCategories
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.NotFoundError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = category.UpdateTagDetails(request.TagId, request.Name, request.Description,
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
            return Result.Failure(IntakeRequestTagCategory.DuplicateTagNameError);
        }

        return Result.Success();
    }
}
