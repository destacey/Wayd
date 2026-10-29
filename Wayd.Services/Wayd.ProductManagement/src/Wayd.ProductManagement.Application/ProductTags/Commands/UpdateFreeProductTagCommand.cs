using Wayd.Common.Domain.Tagging;

namespace Wayd.ProductManagement.Application.ProductTags.Commands;

/// <summary>
/// Renames a free product tag or changes its description. Every product carrying it reads the new name at once.
/// </summary>
/// <remarks>
/// To fold a free tag into one that already has the name, merge it instead.
/// </remarks>
public sealed record UpdateFreeProductTagCommand(Guid Id, string Name, string? Description) : ICommand;

public sealed class UpdateFreeProductTagCommandValidator : AbstractValidator<UpdateFreeProductTagCommand>
{
    public UpdateFreeProductTagCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(TagBase.NameMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(512);
    }
}

public sealed class UpdateFreeProductTagCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<UpdateFreeProductTagCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateFreeProductTagCommand>
{
    private const string AppRequestName = nameof(UpdateFreeProductTagCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<UpdateFreeProductTagCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateFreeProductTagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tag = await _productManagementDbContext.ProductTags
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
            if (tag is null)
                return Result.Failure(ProductTagErrors.NotFound);

            var name = TagBase.ValidateFreeName(request.Name);
            if (name.IsFailure)
                return name;

            var other = await FreeProductTags.FindByName(_productManagementDbContext, name.Value, cancellationToken);
            if (other is not null && other.Id != tag.Id)
                return Result.Failure(TagErrors.DuplicateFreeTagName);

            var result = tag.UpdateFreeTagDetails(name.Value, request.Description,
                EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken)), _dateTimeProvider.Now);
            if (result.IsFailure)
                return result;

            try
            {
                await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The name was checked above, so a failed save is another free tag taking it meanwhile.
                return Result.Failure(TagErrors.DuplicateFreeTagName);
            }

            _logger.LogInformation("Free product tag {TagId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
