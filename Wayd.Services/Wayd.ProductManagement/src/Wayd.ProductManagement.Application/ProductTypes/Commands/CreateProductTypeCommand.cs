using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ProductTypes.Commands;

public sealed record CreateProductTypeCommand(
    string Name,
    string? Description,
    bool IsReleasable,
    int Order) : ICommand<ObjectIdAndKey>;

public sealed class CreateProductTypeCommandValidator : AbstractValidator<CreateProductTypeCommand>
{
    public CreateProductTypeCommandValidator()
    {
        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .MaximumLength(512);

        RuleFor(t => t.Order)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateProductTypeCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<CreateProductTypeCommandHandler> logger)
    : ICommandHandler<CreateProductTypeCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateProductTypeCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<CreateProductTypeCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(
        CreateProductTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var name = request.Name.Trim();

            // Checked here as well as by the unique index, so a duplicate is a message rather than a
            // DbUpdateException the caller cannot read.
            if (await _productManagementDbContext.ProductTypes.AnyAsync(t => t.Name == name, cancellationToken))
            {
                return Result.Failure<ObjectIdAndKey>($"A product type named '{name}' already exists.");
            }

            var productType = ProductType.Create(name, request.Description, request.IsReleasable, request.Order);

            await _productManagementDbContext.ProductTypes.AddAsync(productType, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product Type {ProductTypeId} created.", productType.Id);

            return Result.Success(new ObjectIdAndKey(productType.Id, productType.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
