using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Freezes scope and marks a release ready to ship.
/// </summary>
/// <remarks>
/// Cutting is one-way: a release already cut, released or withdrawn refuses it.
/// </remarks>
public sealed record CutReleaseCommand(Guid Id, LocalDate CutDate) : ICommand;

public sealed class CutReleaseCommandValidator : AbstractValidator<CutReleaseCommand>
{
    public CutReleaseCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class CutReleaseCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<CutReleaseCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CutReleaseCommand>
{
    private const string AppRequestName = nameof(CutReleaseCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CutReleaseCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(CutReleaseCommand request, CancellationToken cancellationToken)
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

            // The aggregate demands the status carrying this meaning and cannot fetch it. Resolving by
            // alias rather than by id is what lets an organization rename or reorder its workflow
            // without breaking the transition.
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

            var productName = await _productManagementDbContext.Products
                .Where(p => p.Id == release.ProductId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var result = release.Cut(
                request.CutDate,
                status.Value,
                productName,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to cut Release {ReleaseId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} cut.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
