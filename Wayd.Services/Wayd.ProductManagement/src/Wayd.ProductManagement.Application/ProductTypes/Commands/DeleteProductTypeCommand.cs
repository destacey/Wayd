namespace Wayd.ProductManagement.Application.ProductTypes.Commands;

/// <summary>
/// Permanently removes an unused product type.
/// </summary>
/// <remarks>
/// Only for a type nothing carries. A type in use is deactivated instead, so the products holding it
/// keep resolving what they are.
/// </remarks>
public sealed record DeleteProductTypeCommand(Guid Id) : ICommand;

public sealed class DeleteProductTypeCommandValidator : AbstractValidator<DeleteProductTypeCommand>
{
    public DeleteProductTypeCommandValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty();
    }
}

public sealed class DeleteProductTypeCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<DeleteProductTypeCommandHandler> logger)
    : ICommandHandler<DeleteProductTypeCommand>
{
    private const string AppRequestName = nameof(DeleteProductTypeCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<DeleteProductTypeCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteProductTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var productType = await _productManagementDbContext.ProductTypes
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            if (productType is null)
            {
                _logger.LogInformation("Product Type {ProductTypeId} not found.", request.Id);
                return Result.Failure("Product Type not found.");
            }

            if (productType.IsSystem)
            {
                return Result.Failure("System product types cannot be deleted. Deactivate it instead.");
            }

            if (await _productManagementDbContext.Products.AnyAsync(p => p.ProductTypeId == request.Id, cancellationToken))
            {
                return Result.Failure("This product type is in use and cannot be deleted. Deactivate it instead.");
            }

            _productManagementDbContext.ProductTypes.Remove(productType);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product Type {ProductTypeId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
