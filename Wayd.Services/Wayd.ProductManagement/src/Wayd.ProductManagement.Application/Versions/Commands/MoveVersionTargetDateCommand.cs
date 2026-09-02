namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Moves or clears a version's target date.
/// </summary>
/// <remarks>
/// Separate from the details update because a slipping date is what stakeholders watch: the aggregate
/// raises an event carrying both the old and new dates, which a blanket field update would bury.
/// </remarks>
public sealed record MoveVersionTargetDateCommand(Guid Id, LocalDate? TargetDate) : ICommand;

public sealed class MoveVersionTargetDateCommandValidator : AbstractValidator<MoveVersionTargetDateCommand>
{
    public MoveVersionTargetDateCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class MoveVersionTargetDateCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<MoveVersionTargetDateCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MoveVersionTargetDateCommand>
{
    private const string AppRequestName = nameof(MoveVersionTargetDateCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<MoveVersionTargetDateCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MoveVersionTargetDateCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var version = await _productManagementDbContext.Versions
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (version is null)
            {
                _logger.LogInformation("Version {VersionId} not found.", request.Id);
                return Result.Failure("Version not found.");
            }

            var productName = await _productManagementDbContext.Products
                .Where(p => p.Id == version.ProductId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var result = version.MoveTargetDate(
                request.TargetDate, productName, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                version.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to move Version {VersionId} target date. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Version {VersionId} target date moved.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
