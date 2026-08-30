using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// Pulls a package after it was assembled.
/// </summary>
/// <remarks>
/// The package is kept: deployments may reference it, and the delivery measures read that history.
/// </remarks>
public sealed record WithdrawReleasePackageCommand(Guid Id, string? Reason) : ICommand;

public sealed class WithdrawReleasePackageCommandValidator : AbstractValidator<WithdrawReleasePackageCommand>
{
    public WithdrawReleasePackageCommandValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.Reason)
            .MaximumLength(1024);
    }
}

public sealed class WithdrawReleasePackageCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<WithdrawReleasePackageCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<WithdrawReleasePackageCommand>
{
    private const string AppRequestName = nameof(WithdrawReleasePackageCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<WithdrawReleasePackageCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(WithdrawReleasePackageCommand request, CancellationToken cancellationToken)
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

            var status = await _statusResolver.ForAlias(
                ProductWorkflowOwners.ReleasePackage.Key,
                scopeId: null,
                (int)ProductStatusAlias.Withdrawn,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the withdrawn package status. Error message: {Error}", status.Error);
                return Result.Failure(status.Error);
            }

            var result = package.Withdraw(
                request.Reason,
                status.Value,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                package.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to withdraw Release Package {PackageId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release Package {PackageId} withdrawn.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
