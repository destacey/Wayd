namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Edits a release's version, name, notes and ordering sequence.
/// </summary>
/// <remarks>
/// Dates move through their own commands, because cutting and shipping are status transitions with
/// rules, not fields.
/// </remarks>
public sealed record UpdateReleaseDetailsCommand(
    Guid Id,
    string Version,
    string? Name,
    string? Notes,
    long? Sequence) : ICommand;

public sealed class UpdateReleaseDetailsCommandValidator : AbstractValidator<UpdateReleaseDetailsCommand>
{
    public UpdateReleaseDetailsCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);

        RuleFor(r => r.Notes)
            .MaximumLength(4000);
    }
}

public sealed class UpdateReleaseDetailsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<UpdateReleaseDetailsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateReleaseDetailsCommand>
{
    private const string AppRequestName = nameof(UpdateReleaseDetailsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateReleaseDetailsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateReleaseDetailsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var release = await _productManagementDbContext.Releases
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (release is null)
            {
                _logger.LogInformation("Release {ReleaseId} not found.", request.Id);
                return Result.Failure("Release not found.");
            }

            var result = release.UpdateDetails(
                request.Version,
                request.Name,
                request.Notes,
                request.Sequence,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to update Release {ReleaseId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
