namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Edits a version's version, name, notes and ordering sequence.
/// </summary>
/// <remarks>
/// Dates move through their own commands, because cutting and shipping are status transitions with
/// rules, not fields.
/// </remarks>
public sealed record UpdateVersionDetailsCommand(
    Guid Id,
    string Version,
    string? Name,
    string? Notes,
    long? Sequence) : ICommand;

public sealed class UpdateVersionDetailsCommandValidator : AbstractValidator<UpdateVersionDetailsCommand>
{
    public UpdateVersionDetailsCommandValidator()
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

public sealed class UpdateVersionDetailsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<UpdateVersionDetailsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateVersionDetailsCommand>
{
    private const string AppRequestName = nameof(UpdateVersionDetailsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateVersionDetailsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateVersionDetailsCommand request, CancellationToken cancellationToken)
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

            var result = version.UpdateDetails(
                request.Version,
                request.Name,
                request.Notes,
                request.Sequence,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                version.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to update Version {VersionId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Version {VersionId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
