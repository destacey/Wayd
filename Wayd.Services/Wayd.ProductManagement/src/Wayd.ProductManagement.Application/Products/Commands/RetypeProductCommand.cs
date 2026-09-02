namespace Wayd.ProductManagement.Application.Products.Commands;

public sealed record RetypeProductCommand(Guid Id, Guid ProductTypeId) : ICommand;

public sealed class RetypeProductCommandValidator : AbstractValidator<RetypeProductCommand>
{
    public RetypeProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.ProductTypeId)
            .NotEmpty();
    }
}

public sealed class RetypeProductCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<RetypeProductCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RetypeProductCommand>
{
    private const string AppRequestName = nameof(RetypeProductCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RetypeProductCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RetypeProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productManagementDbContext.Products
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.Id);
                return Result.Failure("Product not found.");
            }

            var productType = await _productManagementDbContext.ProductTypes
                .FirstOrDefaultAsync(t => t.Id == request.ProductTypeId, cancellationToken);

            if (productType is null)
            {
                _logger.LogInformation("Product Type {ProductTypeId} not found.", request.ProductTypeId);
                return Result.Failure("Product Type not found.");
            }

            if (!productType.IsActive && productType.Id != product.ProductTypeId)
            {
                return Result.Failure($"'{productType.Name}' is inactive and cannot be assigned.");
            }

            // Retype refuses to move a node onto a non-releasable type once versions exist; the query
            // is the handler's because the aggregate cannot run it.
            //
            // Versions, not Releases: releasability gates the artifact, and a release is an
            // announcement that may sit under any node.
            var hasVersions = await _productManagementDbContext.Versions
                .AnyAsync(v => v.ProductId == request.Id, cancellationToken);

            var retypeResult = product.Retype(
                request.ProductTypeId,
                productType.IsReleasable,
                hasVersions,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (retypeResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is
                // touched, so there is nothing to roll back.
                product.ClearDomainEvents();

                _logger.LogError("Unable to retype Product {ProductId}. Error message: {Error}", request.Id, retypeResult.Error);
                return Result.Failure(retypeResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} retyped to {ProductTypeId}.", request.Id, request.ProductTypeId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
