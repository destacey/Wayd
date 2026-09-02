using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Records that a release marked as shipped did not in fact ship.
/// </summary>
/// <remarks>
/// Not a withdrawal. Withdrawing says a real release was pulled and is a terminal state; this says the
/// release never happened and the record was wrong, so it moves backward and the release stays live.
/// Using withdrawal for this would leave the history asserting a withdrawal nobody performed.
/// <para>
/// The release returns to Ready where it was cut, and to its workflow's initial status otherwise —
/// a release can be marked released without ever being cut, so there is not always a Ready to go back
/// to.
/// </para>
/// </remarks>
public sealed record RevertReleaseCommand(Guid Id, string Reason) : ICommand, IRequireLinkedEmployee;

public sealed class RevertReleaseCommandValidator : AbstractValidator<RevertReleaseCommand>
{
    public RevertReleaseCommandValidator()
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

public sealed class RevertReleaseCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<RevertReleaseCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RevertReleaseCommand>
{
    private const string AppRequestName = nameof(RevertReleaseCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<RevertReleaseCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RevertReleaseCommand request, CancellationToken cancellationToken)
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

            // Where the release was cut, Ready is the state it was in before shipping. Where it was
            // not — a release entered after the fact, which the domain permits — there is no Ready to
            // return to, so it goes back to the start of its workflow.
            var status = release.CutDate is not null
                ? await _statusResolver.ForAlias(
                    ProductWorkflowOwners.Release.Key,
                    scopeId: null,
                    (int)ProductStatusAlias.Ready,
                    cancellationToken)
                : await _statusResolver.Initial(
                    ProductWorkflowOwners.Release.Key,
                    scopeId: null,
                    cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError(
                    "Unable to resolve the status to revert Release {ReleaseId} to. Error message: {Error}",
                    request.Id, status.Error);
                return Result.Failure(status.Error);
            }

            var productName = await _productManagementDbContext.Products
                .Where(p => p.Id == release.ProductId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. This value is frozen onto the transition, so a stale
            // one would misattribute the change permanently.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.RevertRelease(
                status.Value,
                request.Reason,
                productName,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to revert Release {ReleaseId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} reverted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
