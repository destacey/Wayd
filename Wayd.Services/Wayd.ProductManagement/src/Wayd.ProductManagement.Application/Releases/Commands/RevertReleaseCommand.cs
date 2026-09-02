using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Records that a release marked as announced was not in fact announced.
/// </summary>
/// <remarks>
/// Not a withdrawal. Withdrawing says a real announcement was retracted; this says the announcement
/// never happened and the record was wrong. A reason is required, because this contradicts what the
/// append-only history already asserts.
/// </remarks>
public sealed record RevertReleaseCommand(Guid Id, string Reason) : ICommand, IRequireLinkedEmployee;

public sealed class RevertReleaseCommandValidator : AbstractValidator<RevertReleaseCommand>
{
    public RevertReleaseCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

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

            // Back to Ready, the workflow's non-terminal resting state, rather than to the initial
            // status: a release that was announced had been ready to announce, and reverting says only
            // that the announcement did not happen.
            var status = await _statusResolver.ForAlias(
                ProductWorkflowOwners.Release.Key,
                scopeId: null,
                (int)ProductStatusAlias.Ready,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the ready release status. Error message: {Error}", status.Error);
                return Result.Failure(status.Error);
            }

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.RevertRelease(
                status.Value,
                request.Reason,
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
