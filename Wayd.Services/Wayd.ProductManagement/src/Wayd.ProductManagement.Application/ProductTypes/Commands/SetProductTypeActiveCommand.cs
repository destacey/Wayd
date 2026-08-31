namespace Wayd.ProductManagement.Application.ProductTypes.Commands;

/// <summary>
/// Takes a product type out of use, or puts it back.
/// </summary>
/// <remarks>
/// Deactivation rather than deletion is the reversible move, and it says nothing about products already
/// using the type — they keep resolving it. Seeded types can be deactivated too: an organization that
/// does not ship libraries should be able to hide the type without the seeder recreating it.
/// </remarks>
public sealed record SetProductTypeActiveCommand(Guid Id, bool IsActive) : ICommand;

public sealed class SetProductTypeActiveCommandValidator : AbstractValidator<SetProductTypeActiveCommand>
{
    public SetProductTypeActiveCommandValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty();
    }
}

public sealed class SetProductTypeActiveCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<SetProductTypeActiveCommandHandler> logger)
    : ICommandHandler<SetProductTypeActiveCommand>
{
    private const string AppRequestName = nameof(SetProductTypeActiveCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<SetProductTypeActiveCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetProductTypeActiveCommand request, CancellationToken cancellationToken)
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

            var result = request.IsActive ? productType.Activate() : productType.Deactivate();

            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to change Product Type {ProductTypeId} activation. Error message: {Error}",
                    request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Product Type {ProductTypeId} active set to {IsActive}.", request.Id, request.IsActive);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
