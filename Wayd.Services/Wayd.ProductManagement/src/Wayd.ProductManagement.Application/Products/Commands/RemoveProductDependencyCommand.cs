namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Deletes a dependency recorded on a product by mistake.
/// </summary>
/// <remarks>
/// Not for a dependency that stopped — <see cref="EndProductDependencyCommand"/> keeps that history.
/// </remarks>
public sealed record RemoveProductDependencyCommand(Guid Id, Guid DependencyId, string Reason) : ICommand;

public sealed class RemoveProductDependencyCommandValidator : AbstractValidator<RemoveProductDependencyCommand>
{
    public RemoveProductDependencyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.DependencyId)
            .NotEmpty();

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

public sealed class RemoveProductDependencyCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<RemoveProductDependencyCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RemoveProductDependencyCommand>
{
    private const string AppRequestName = nameof(RemoveProductDependencyCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RemoveProductDependencyCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RemoveProductDependencyCommand request, CancellationToken cancellationToken)
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

            var removeResult = product.RemoveDependency(
                request.DependencyId,
                request.Reason,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (removeResult.IsFailure)
            {
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to remove dependency {DependencyId} from Product {ProductId}. Error message: {Error}", request.DependencyId, request.Id, removeResult.Error);
                return Result.Failure(removeResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Dependency {DependencyId} removed from Product {ProductId}.", request.DependencyId, request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
