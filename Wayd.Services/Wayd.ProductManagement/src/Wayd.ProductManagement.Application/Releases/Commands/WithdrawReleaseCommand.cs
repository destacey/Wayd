using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Retracts a release after it was announced.
/// </summary>
/// <remarks>
/// Says nothing about the versions it carried. An artifact that shipped has shipped whatever the
/// market was later told, so each version is withdrawn separately where it too was pulled.
/// </remarks>
public sealed record WithdrawReleaseCommand(Guid Id, string? Reason) : ICommand, IRequireLinkedEmployee;

public sealed class WithdrawReleaseCommandValidator : AbstractValidator<WithdrawReleaseCommand>
{
    public WithdrawReleaseCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => r.Reason)
            .MaximumLength(1024);
    }
}

public sealed class WithdrawReleaseCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<WithdrawReleaseCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<WithdrawReleaseCommand>
{
    private const string AppRequestName = nameof(WithdrawReleaseCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<WithdrawReleaseCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(WithdrawReleaseCommand request, CancellationToken cancellationToken)
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

            var status = await _statusResolver.ForAlias(
                ProductWorkflowOwners.Release.Key,
                scopeId: null,
                (int)ProductStatusAlias.Withdrawn,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the withdrawn release status. Error message: {Error}", status.Error);
                return Result.Failure(status.Error);
            }

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. This value is frozen onto the transition, so a stale
            // one would misattribute the change permanently.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.Withdraw(
                request.Reason,
                status.Value,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to withdraw Release {ReleaseId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} withdrawn.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
