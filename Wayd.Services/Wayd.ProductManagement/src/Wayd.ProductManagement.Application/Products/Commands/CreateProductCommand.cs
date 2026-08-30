using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Commands;

public sealed record CreateProductCommand(
    string Name,
    string? Description,
    Guid ProductTypeId,
    Guid? ParentId,
    string? ExternalId) : ICommand<ObjectIdAndKey>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(1024);

        RuleFor(x => x.ProductTypeId)
            .NotEmpty();

        RuleFor(x => x.ExternalId)
            .MaximumLength(256);
    }
}

public sealed class CreateProductCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<CreateProductCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateProductCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateProductCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CreateProductCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var productType = await _productManagementDbContext.ProductTypes
                .FirstOrDefaultAsync(t => t.Id == request.ProductTypeId, cancellationToken);

            if (productType is null)
            {
                _logger.LogInformation("Product Type {ProductTypeId} not found.", request.ProductTypeId);
                return Result.Failure<ObjectIdAndKey>("Product Type not found.");
            }

            if (!productType.IsActive)
            {
                return Result.Failure<ObjectIdAndKey>($"'{productType.Name}' is inactive and cannot be assigned to a new product.");
            }

            if (request.ParentId is not null
                && !await _productManagementDbContext.Products.AnyAsync(p => p.Id == request.ParentId, cancellationToken))
            {
                _logger.LogInformation("Parent Product {ParentId} not found.", request.ParentId);
                return Result.Failure<ObjectIdAndKey>("Parent product not found.");
            }

            // Product Management assigns workflows organization-wide, so the scope is null. A module
            // with a container passes its id here instead.
            var initialStatus = await _statusResolver.Initial(
                ProductWorkflowOwners.Product.Key,
                scopeId: null,
                cancellationToken);

            if (initialStatus.IsFailure)
            {
                _logger.LogError("Unable to resolve the initial product status. Error message: {Error}", initialStatus.Error);
                return Result.Failure<ObjectIdAndKey>(initialStatus.Error);
            }

            var product = Product.Create(
                request.Name,
                request.Description,
                request.ProductTypeId,
                request.ParentId,
                request.ExternalId,
                initialStatus.Value,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            await _productManagementDbContext.Products.AddAsync(product, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} created.", product.Id);

            return Result.Success(new ObjectIdAndKey(product.Id, product.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
