using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Pulls a version after it was cut.
/// </summary>
/// <remarks>
/// The version is not deleted: it was real, deployments may reference it, and the delivery measures
/// read that history.
/// </remarks>
public sealed record WithdrawVersionCommand(Guid Id, string? Reason) : ICommand, IRequireLinkedEmployee;

public sealed class WithdrawVersionCommandValidator : AbstractValidator<WithdrawVersionCommand>
{
    public WithdrawVersionCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => r.Reason)
            .MaximumLength(1024);
    }
}

public sealed class WithdrawVersionCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<WithdrawVersionCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<WithdrawVersionCommand>
{
    private const string AppRequestName = nameof(WithdrawVersionCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<WithdrawVersionCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(WithdrawVersionCommand request, CancellationToken cancellationToken)
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
                (int)ProductStatusAlias.Withdrawn,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the withdrawn version status. Error message: {Error}", status.Error);
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

            var result = version.Withdraw(
                request.Reason,
                status.Value,
                productName,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                version.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to withdraw Version {VersionId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Version {VersionId} withdrawn.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
