using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Freezes scope and marks a version ready to ship.
/// </summary>
/// <remarks>
/// Cutting is one-way: a version already cut, released or withdrawn refuses it.
/// </remarks>
public sealed record CutVersionCommand(Guid Id, LocalDate CutDate) : ICommand, IRequireLinkedEmployee;

public sealed class CutVersionCommandValidator : AbstractValidator<CutVersionCommand>
{
    public CutVersionCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class CutVersionCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<CutVersionCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CutVersionCommand>
{
    private const string AppRequestName = nameof(CutVersionCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<CutVersionCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(CutVersionCommand request, CancellationToken cancellationToken)
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
                (int)ProductStatusAlias.Ready,
                cancellationToken);

            if (status.IsFailure)
            {
                _logger.LogError("Unable to resolve the ready version status. Error message: {Error}", status.Error);
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

            var result = version.Cut(
                request.CutDate,
                status.Value,
                productName,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                version.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to cut Version {VersionId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Version {VersionId} cut.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
