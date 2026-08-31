using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ProductTagCategories.Commands;

/// <summary>
/// Creates a tag axis.
/// </summary>
/// <param name="AllowsMany">
/// Whether a product can carry several tags from this axis. True suits Platform, where a cross-platform
/// app genuinely targets both iOS and Android; false suits an axis where a node can only be one thing.
/// <strong>Fixed once set</strong> — narrowing it later would leave products holding more tags than the
/// axis permits, and the domain has no rule for choosing which to drop.
/// </param>
public sealed record CreateProductTagCategoryCommand(
    string Name,
    string? Description,
    bool AllowsMany,
    int Order) : ICommand<ObjectIdAndKey>;

public sealed class CreateProductTagCategoryCommandValidator : AbstractValidator<CreateProductTagCategoryCommand>
{
    public CreateProductTagCategoryCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.Description)
            .MaximumLength(512);

        RuleFor(c => c.Order)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateProductTagCategoryCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ILogger<CreateProductTagCategoryCommandHandler> logger)
    : ICommandHandler<CreateProductTagCategoryCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateProductTagCategoryCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ILogger<CreateProductTagCategoryCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(
        CreateProductTagCategoryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var name = request.Name.Trim();

            if (await _productManagementDbContext.ProductTagCategories.AnyAsync(c => c.Name == name, cancellationToken))
            {
                return Result.Failure<ObjectIdAndKey>($"A tag category named '{name}' already exists.");
            }

            var category = ProductTagCategory.Create(name, request.Description, request.AllowsMany, request.Order);

            await _productManagementDbContext.ProductTagCategories.AddAsync(category, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product Tag Category {CategoryId} created.", category.Id);

            return Result.Success(new ObjectIdAndKey(category.Id, category.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
