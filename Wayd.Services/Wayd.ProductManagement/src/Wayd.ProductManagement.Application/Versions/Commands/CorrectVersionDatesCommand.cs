namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Corrects a version's recorded target, cut and released dates.
/// </summary>
/// <remarks>
/// Separate from cutting and releasing, which assert that the version moved and so refuse to run
/// twice. This asserts only that a date was written down wrongly, and leaves the status alone — the
/// alternative was to withdraw a version and version it again, which writes two status transitions
/// that never happened.
/// <para>
/// Every date is sent, so an omitted one clears it. The target and cut dates may be added, changed or
/// cleared freely; the released date may be added or changed but not cleared, because emptying it
/// would leave a released record contradicting its own status —
/// <c>RevertVersionReleaseCommand</c> is the action for that.
/// </para>
/// </remarks>
public sealed record CorrectVersionDatesCommand(
    Guid Id,
    LocalDate? TargetDate,
    LocalDate? CutDate,
    LocalDate? ReleasedDate)
    : ICommand, IRequireLinkedEmployee;

public sealed class CorrectVersionDatesCommandValidator : AbstractValidator<CorrectVersionDatesCommand>
{
    public CorrectVersionDatesCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class CorrectVersionDatesCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<CorrectVersionDatesCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CorrectVersionDatesCommand>
{
    private const string AppRequestName = nameof(CorrectVersionDatesCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<CorrectVersionDatesCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(CorrectVersionDatesCommand request, CancellationToken cancellationToken)
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

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. The correction's actor is permanent audit-trail data.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = version.CorrectDates(
                request.TargetDate,
                request.CutDate,
                request.ReleasedDate,
                productName,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                version.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to correct Version {VersionId} dates. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Version {VersionId} dates corrected.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
