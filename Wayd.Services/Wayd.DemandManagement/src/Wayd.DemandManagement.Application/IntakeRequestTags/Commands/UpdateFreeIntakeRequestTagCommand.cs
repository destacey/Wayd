using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tagging;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;

namespace Wayd.DemandManagement.Application.IntakeRequestTags.Commands;

/// <summary>
/// Renames a free intake request tag or changes its description. The Administer permission is checked by the
/// endpoint. Every request carrying it reads the new name at once; to fold it into a tag that already has the
/// name, merge it instead.
/// </summary>
/// <param name="Id">The free tag.</param>
/// <param name="Name">The new name, unique among free tags ignoring case.</param>
/// <param name="Description">What the tag means, where it is not obvious.</param>
public sealed record UpdateFreeIntakeRequestTagCommand(Guid Id, string Name, string? Description) : ICommand;

public sealed class UpdateFreeIntakeRequestTagCommandValidator : CustomValidator<UpdateFreeIntakeRequestTagCommand>
{
    public UpdateFreeIntakeRequestTagCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(TagBase.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(512);
    }
}

public sealed class UpdateFreeIntakeRequestTagCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateFreeIntakeRequestTagCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateFreeIntakeRequestTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await _dbContext.IntakeRequestTags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (tag is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.TagNotFoundError);

        var name = TagBase.ValidateFreeName(request.Name);
        if (name.IsFailure)
            return name;

        var other = await FreeIntakeRequestTags.FindByName(_dbContext, name.Value, cancellationToken);
        if (other is not null && other.Id != tag.Id)
            return Result.Failure(TagErrors.DuplicateFreeTagName);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = tag.UpdateFreeTagDetails(name.Value, request.Description,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The name was checked above, so a failed save is another free tag taking it meanwhile.
            return Result.Failure(TagErrors.DuplicateFreeTagName);
        }

        return Result.Success();
    }
}
