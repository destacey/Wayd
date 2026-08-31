namespace Wayd.ProductManagement.Application.Products.Commands;

public sealed record RemoveProductCommand(Guid Id) : ICommand;

public sealed class RemoveProductCommandValidator : AbstractValidator<RemoveProductCommand>
{
    public RemoveProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class RemoveProductCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<RemoveProductCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RemoveProductCommand>
{
    private const string AppRequestName = nameof(RemoveProductCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RemoveProductCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RemoveProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productManagementDbContext.Products
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.Id);
                return Result.Failure("Product not found.");
            }

            var hasChildren = await _productManagementDbContext.Products
                .AnyAsync(p => p.ParentId == request.Id, cancellationToken);

            var hasReleases = await _productManagementDbContext.Releases
                .AnyAsync(r => r.ProductId == request.Id, cancellationToken);

            // Checked separately from releases: ReleasePackageComponents.ProductId restricts, and a
            // carried-forward component often has no release row, so the check above would miss it and
            // the delete would fail at the database with a generic message.
            var isInAManifest = await _productManagementDbContext.ReleasePackageComponents
                .AnyAsync(c => c.ProductId == request.Id, cancellationToken);

            // Raises the event; the delete itself is the handler's, since the aggregate cannot remove
            // itself from a set it does not know about.
            var removeResult = product.Remove(
                hasChildren,
                hasReleases,
                isInAManifest,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (removeResult.IsFailure)
            {
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to remove Product {ProductId}. Error message: {Error}", request.Id, removeResult.Error);
                return Result.Failure(removeResult.Error);
            }

            _productManagementDbContext.Products.Remove(product);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} removed.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
