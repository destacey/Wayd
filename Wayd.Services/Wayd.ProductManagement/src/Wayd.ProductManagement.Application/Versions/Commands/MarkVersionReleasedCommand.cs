using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Records that a version shipped.
/// </summary>
/// <remarks>
/// The released date is what orders a version history, so it is supplied rather than taken from the
/// clock: shipping is often recorded after the fact.
/// </remarks>
public sealed record MarkVersionReleasedCommand(Guid Id, LocalDate ReleasedDate) : ICommand, IRequireLinkedEmployee;

public sealed class MarkVersionReleasedCommandValidator : AbstractValidator<MarkVersionReleasedCommand>
{
    public MarkVersionReleasedCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class MarkVersionReleasedCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<MarkVersionReleasedCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkVersionReleasedCommand>
{
    private const string AppRequestName = nameof(MarkVersionReleasedCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<MarkVersionReleasedCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MarkVersionReleasedCommand request, CancellationToken cancellationToken)
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

            // The aggregate demands the status carrying this meaning and cannot fetch it. Resolving by
            // alias rather than by id is what lets an organization rename or reorder its workflow
            // without breaking the transition.
            var status = await _statusResolver.ForAlias(
                ProductWorkflowOwners.Version.Key,
                scopeId: null,
                (int)ProductStatusAlias.Released,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the released version status. Error message: {Error}", status.Error);
                return Result.Failure(status.Error);
            }

            var productName = await _productManagementDbContext.Products
                .Where(p => p.Id == version.ProductId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. This value is frozen onto the transition, so a stale
            // one would misattribute the change permanently.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = version.MarkReleased(
                request.ReleasedDate,
                status.Value,
                productName,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                version.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to mark released Version {VersionId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Version {VersionId} marked released.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
