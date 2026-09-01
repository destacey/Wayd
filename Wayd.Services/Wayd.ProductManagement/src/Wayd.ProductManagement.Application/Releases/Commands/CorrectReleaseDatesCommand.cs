namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Corrects a release's recorded cut and released dates.
/// </summary>
/// <remarks>
/// Separate from cutting and releasing, which assert that the release moved and so refuse to run
/// twice. This asserts only that a date was written down wrongly, and leaves the status alone — the
/// alternative was to withdraw a release and release it again, which writes two status transitions
/// that never happened.
/// </remarks>
public sealed record CorrectReleaseDatesCommand(Guid Id, LocalDate? CutDate, LocalDate? ReleasedDate)
    : ICommand, IRequireLinkedEmployee;

public sealed class CorrectReleaseDatesCommandValidator : AbstractValidator<CorrectReleaseDatesCommand>
{
    public CorrectReleaseDatesCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class CorrectReleaseDatesCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<CorrectReleaseDatesCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CorrectReleaseDatesCommand>
{
    private const string AppRequestName = nameof(CorrectReleaseDatesCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<CorrectReleaseDatesCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(CorrectReleaseDatesCommand request, CancellationToken cancellationToken)
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

            var productName = await _productManagementDbContext.Products
                .Where(p => p.Id == release.ProductId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. The correction's actor is permanent audit-trail data.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.CorrectDates(
                request.CutDate,
                request.ReleasedDate,
                productName,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to correct Release {ReleaseId} dates. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} dates corrected.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
