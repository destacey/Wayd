using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Plans a version against a product.
/// </summary>
/// <param name="Version">
/// The version as the organization writes it. <strong>Free text, never parsed</strong> — nothing sorts
/// or compares it.
/// </param>
/// <param name="Sequence">
/// A manual ordering override, for the rare case where chronology misleads.
/// </param>
public sealed record PlanVersionCommand(
    Guid ProductId,
    string Version,
    string? Name,
    LocalDate? TargetDate,
    long? Sequence) : ICommand<ObjectIdAndKey>;

public sealed class PlanVersionCommandValidator : AbstractValidator<PlanVersionCommand>
{
    public PlanVersionCommandValidator()
    {
        RuleFor(r => r.ProductId)
            .NotEmpty();

        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);
    }
}

public sealed class PlanVersionCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<PlanVersionCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PlanVersionCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(PlanVersionCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<PlanVersionCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(PlanVersionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productManagementDbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.ProductId);
                return Result.Failure<ObjectIdAndKey>("Product not found.");
            }

            // Whether the product's type permits versions is the type's to answer, and the aggregate
            // cannot load it.
            //
            // The type is loaded rather than projecting IsReleasable straight out: FirstOrDefaultAsync
            // over a bool cannot tell "not releasable" from "no such type", so a dangling type id would
            // surface as a releasability refusal and send the caller looking in the wrong place.
            var productType = await _productManagementDbContext.ProductTypes
                .FirstOrDefaultAsync(t => t.Id == product.ProductTypeId, cancellationToken);

            if (productType is null)
            {
                _logger.LogError(
                    "Product {ProductId} references Product Type {ProductTypeId}, which does not exist.",
                    request.ProductId,
                    product.ProductTypeId);
                return Result.Failure<ObjectIdAndKey>("Product Type not found.");
            }

            var isReleasable = productType.IsReleasable;

            var initialStatus = await _statusResolver.Initial(
                ProductWorkflowOwners.Version.Key, scopeId: null, cancellationToken);

            if (initialStatus.IsFailure)
            {
                _logger.LogError("Unable to resolve the initial version status. Error message: {Error}", initialStatus.Error);
                return Result.Failure<ObjectIdAndKey>(initialStatus.Error);
            }

            var result = Version.Create(
                request.ProductId,
                request.Version,
                request.Name,
                request.TargetDate,
                request.Sequence,
                isReleasable,
                initialStatus.Value,
                product.Name,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to plan a version for {ProductId}. Error message: {Error}", request.ProductId, result.Error);
                return Result.Failure<ObjectIdAndKey>(result.Error);
            }

            await _productManagementDbContext.Versions.AddAsync(result.Value, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Version {VersionId} planned.", result.Value.Id);

            return Result.Success(new ObjectIdAndKey(result.Value.Id, result.Value.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
