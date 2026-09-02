namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Updates a release's version label, name, notes, owning product or ordering sequence.
/// </summary>
public sealed record UpdateReleaseDetailsCommand(
    Guid Id,
    string Version,
    string? Name,
    string? Notes,
    Guid? ProductId,
    long? Sequence) : ICommand, IRequireLinkedEmployee;

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
    ICurrentPrincipal currentPrincipal,
    ILogger<UpdateReleaseDetailsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateReleaseDetailsCommand>
{
    private const string AppRequestName = nameof(UpdateReleaseDetailsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
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

            if (request.ProductId is not null
                && !await _productManagementDbContext.Products
                    .AnyAsync(p => p.Id == request.ProductId, cancellationToken))
            {
                _logger.LogInformation("Product {ProductId} not found.", request.ProductId);
                return Result.Failure("Product not found.");
            }

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.UpdateDetails(
                request.Version,
                request.Name,
                request.Notes,
                request.ProductId,
                request.Sequence,
                EventActor.User(_currentUser.GetUserId(), employeeId),
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
