namespace Wayd.ProductManagement.Application.ProductTypes.Commands;

/// <summary>
/// Edits a product type, including whether nodes of this type can carry releases.
/// </summary>
/// <remarks>
/// Changing <see cref="IsReleasable"/> to false refuses <em>new</em> releases only; those already cut
/// stand as historical records. A product whose type stops being releasable keeps its releases.
/// </remarks>
public sealed record UpdateProductTypeCommand(
    Guid Id,
    string Name,
    string? Description,
    bool IsReleasable,
    int Order) : ICommand;

public sealed class UpdateProductTypeCommandValidator : AbstractValidator<UpdateProductTypeCommand>
{
    public UpdateProductTypeCommandValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty();

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .MaximumLength(512);

        RuleFor(t => t.Order)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductTypeCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<UpdateProductTypeCommandHandler> logger)
    : ICommandHandler<UpdateProductTypeCommand>
{
    private const string AppRequestName = nameof(UpdateProductTypeCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<UpdateProductTypeCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProductTypeCommand request, CancellationToken cancellationToken)
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

            var name = request.Name.Trim();

            if (await _productManagementDbContext.ProductTypes
                    .AnyAsync(t => t.Id != request.Id && t.Name == name, cancellationToken))
            {
                return Result.Failure($"A product type named '{name}' already exists.");
            }

            // Two mutations, so the refusal both share is checked once up front — a name applied by the
            // first call and then abandoned by the second would still be tracked and saved.
            if (productType.IsSystem)
            {
                return Result.Failure("System product types cannot be modified.");
            }

            var updateResult = productType.Update(name, request.Description, request.Order);
            if (updateResult.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to update Product Type {ProductTypeId}. Error message: {Error}", request.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            var releasableResult = productType.SetReleasable(request.IsReleasable);
            if (releasableResult.IsFailure)
            {
                return Result.Failure(releasableResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product Type {ProductTypeId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
