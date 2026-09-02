using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Plans a release — drafts an announcement, before it carries anything.
/// </summary>
/// <param name="ProductId">
/// The product node to announce under, or <c>null</c> where the release spans product lines.
/// </param>
/// <param name="Version">
/// The release's own version label. <strong>Free text, never parsed</strong> — nothing sorts or
/// compares it.
/// </param>
/// <param name="Sequence">
/// A manual ordering override, for the rare case where chronology misleads.
/// </param>
public sealed record PlanReleaseCommand(
    Guid? ProductId,
    string Version,
    string? Name,
    LocalDate? TargetDate,
    long? Sequence) : ICommand<ObjectIdAndKey>;

public sealed class PlanReleaseCommandValidator : AbstractValidator<PlanReleaseCommand>
{
    public PlanReleaseCommandValidator()
    {
        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);
    }
}

public sealed class PlanReleaseCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<PlanReleaseCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PlanReleaseCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(PlanReleaseCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<PlanReleaseCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(PlanReleaseCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Any node may own an announcement, so there is no releasability check here — that gate
            // asks whether an artifact can be cut against a node, which is a version's question. A
            // product line is usually not releasable and is exactly what a release sits under.
            if (request.ProductId is not null
                && !await _productManagementDbContext.Products
                    .AnyAsync(p => p.Id == request.ProductId, cancellationToken))
            {
                _logger.LogInformation("Product {ProductId} not found.", request.ProductId);
                return Result.Failure<ObjectIdAndKey>("Product not found.");
            }

            var initialStatus = await _statusResolver.Initial(
                ProductWorkflowOwners.Release.Key, scopeId: null, cancellationToken);

            if (initialStatus.IsFailure)
            {
                _logger.LogError("Unable to resolve the initial release status. Error message: {Error}", initialStatus.Error);
                return Result.Failure<ObjectIdAndKey>(initialStatus.Error);
            }

            var result = Release.Create(
                request.ProductId,
                request.Version,
                request.Name,
                request.TargetDate,
                request.Sequence,
                initialStatus.Value,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                _logger.LogInformation("Unable to plan a release. Error message: {Error}", result.Error);
                return Result.Failure<ObjectIdAndKey>(result.Error);
            }

            await _productManagementDbContext.Releases.AddAsync(result.Value, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} planned.", result.Value.Id);

            return Result.Success(new ObjectIdAndKey(result.Value.Id, result.Value.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
