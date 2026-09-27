namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// Corrects a package's recorded target date and released moment.
/// </summary>
/// <remarks>
/// Separate from marking the package released, which asserts that it shipped and so refuses to run
/// twice. This asserts only that a value was written down wrongly, and leaves the status alone —
/// otherwise a package released under the wrong moment could only be withdrawn, stranding every
/// deployment of it.
/// <para>
/// Both values are sent, so an omitted target date clears it. The released moment can be changed on a
/// released package but not cleared, and cannot be added to one that has not been released:
/// <c>MarkReleasePackageReleasedCommand</c> records it.
/// </para>
/// </remarks>
public sealed record CorrectReleasePackageDatesCommand(
    Guid Id,
    LocalDate? TargetDate,
    Instant? ReleasedAt)
    : ICommand, IRequireLinkedEmployee;

public sealed class CorrectReleasePackageDatesCommandValidator : AbstractValidator<CorrectReleasePackageDatesCommand>
{
    public CorrectReleasePackageDatesCommandValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();
    }
}

public sealed class CorrectReleasePackageDatesCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<CorrectReleasePackageDatesCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CorrectReleasePackageDatesCommand>
{
    private const string AppRequestName = nameof(CorrectReleasePackageDatesCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<CorrectReleasePackageDatesCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(CorrectReleasePackageDatesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var package = await _productManagementDbContext.ReleasePackages
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (package is null)
            {
                _logger.LogInformation("Release Package {PackageId} not found.", request.Id);
                return Result.Failure("Release package not found.");
            }

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. The correction's actor is permanent audit-trail data.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = package.CorrectDates(
                request.TargetDate,
                request.ReleasedAt,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                package.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to correct Release Package {PackageId} dates. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release Package {PackageId} dates corrected.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
