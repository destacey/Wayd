namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Rewords what a product's dependency is for.
/// </summary>
public sealed record UpdateProductDependencyCommand(Guid Id, Guid DependencyId, string? Description) : ICommand;

public sealed class UpdateProductDependencyCommandValidator : AbstractValidator<UpdateProductDependencyCommand>
{
    public UpdateProductDependencyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.DependencyId)
            .NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}

public sealed class UpdateProductDependencyCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<UpdateProductDependencyCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateProductDependencyCommand>
{
    private const string AppRequestName = nameof(UpdateProductDependencyCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateProductDependencyCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateProductDependencyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productManagementDbContext.Products
                .Include(p => p.Dependencies)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.Id);
                return Result.Failure("Product not found.");
            }

            var updateResult = product.UpdateDependencyDetails(
                request.DependencyId,
                request.Description,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (updateResult.IsFailure)
            {
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to update dependency {DependencyId} on Product {ProductId}. Error message: {Error}", request.DependencyId, request.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Dependency {DependencyId} on Product {ProductId} updated.", request.DependencyId, request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
