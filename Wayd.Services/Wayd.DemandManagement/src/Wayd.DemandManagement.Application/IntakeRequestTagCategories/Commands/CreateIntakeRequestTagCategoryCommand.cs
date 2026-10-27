using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

/// <summary>
/// Adds a tag axis to the end of the list, with no tags. The Administer permission is checked by the endpoint.
/// </summary>
/// <param name="Name">What the company calls the axis. Unique among active and inactive axes.</param>
/// <param name="Description">What the axis is for, shown when choosing tags.</param>
/// <param name="AllowsMany">Whether a request can carry several tags from the axis. Fixed once created.</param>
public sealed record CreateIntakeRequestTagCategoryCommand(string Name, string? Description, bool AllowsMany) : ICommand<Guid>;

public sealed class CreateIntakeRequestTagCategoryCommandValidator : CustomValidator<CreateIntakeRequestTagCategoryCommand>
{
    public CreateIntakeRequestTagCategoryCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(512);
    }
}

public sealed class CreateIntakeRequestTagCategoryCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateIntakeRequestTagCategoryCommand, Guid>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(CreateIntakeRequestTagCategoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _dbContext.IntakeRequestTagCategories.AnyAsync(c => c.Name == name, cancellationToken))
            return Result.Failure<Guid>(IntakeRequestTagCategoryErrors.DuplicateNameError);

        var lastOrder = await _dbContext.IntakeRequestTagCategories.MaxAsync(c => (int?)c.Order, cancellationToken) ?? 0;

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var category = IntakeRequestTagCategory.Create(name, request.Description, request.AllowsMany, lastOrder + 1,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);

        _dbContext.IntakeRequestTagCategories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
