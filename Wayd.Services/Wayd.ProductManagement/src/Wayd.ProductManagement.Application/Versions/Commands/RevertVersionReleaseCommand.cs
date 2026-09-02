using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Records that a version marked as shipped did not in fact ship.
/// </summary>
/// <remarks>
/// Not a withdrawal. Withdrawing says a real version was pulled and is a terminal state; this says the
/// version never happened and the record was wrong, so it moves backward and the version stays live.
/// Using withdrawal for this would leave the history asserting a withdrawal nobody performed.
/// <para>
/// The version returns to Ready where it was cut, and to its workflow's initial status otherwise —
/// a version can be marked released without ever being cut, so there is not always a Ready to go back
/// to.
/// </para>
/// </remarks>
public sealed record RevertVersionReleaseCommand(Guid Id, string Reason) : ICommand, IRequireLinkedEmployee;

public sealed class RevertVersionReleaseCommandValidator : AbstractValidator<RevertVersionReleaseCommand>
{
    public RevertVersionReleaseCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        // Required, unlike a withdrawal's optional reason: this contradicts something the status
        // history already asserts, so the record has to say why.
        RuleFor(r => r.Reason)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

public sealed class RevertVersionReleaseCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<RevertVersionReleaseCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RevertVersionReleaseCommand>
{
    private const string AppRequestName = nameof(RevertVersionReleaseCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<RevertVersionReleaseCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RevertVersionReleaseCommand request, CancellationToken cancellationToken)
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

            // Where the version was cut, Ready is the state it was in before shipping. Where it was
            // not — a version entered after the fact, which the domain permits — there is no Ready to
            // return to, so it goes back to the start of its workflow.
            var status = version.CutDate is not null
                ? await _statusResolver.ForAlias(
                    ProductWorkflowOwners.Version.Key,
                    scopeId: null,
                    (int)ProductStatusAlias.Ready,
                    cancellationToken)
                : await _statusResolver.Initial(
                    ProductWorkflowOwners.Version.Key,
                    scopeId: null,
                    cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError(
                    "Unable to resolve the status to revert Version {VersionId} to. Error message: {Error}",
                    request.Id, status.Error);
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

            var result = version.RevertRelease(
                status.Value,
                request.Reason,
                productName,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                version.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to revert Version {VersionId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Version {VersionId} reverted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
