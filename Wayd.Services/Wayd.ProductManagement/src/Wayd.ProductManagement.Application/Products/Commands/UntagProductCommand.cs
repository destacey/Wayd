namespace Wayd.ProductManagement.Application.Products.Commands;

public sealed record UntagProductCommand(Guid Id, Guid TagId) : ICommand;

public sealed class UntagProductCommandValidator : AbstractValidator<UntagProductCommand>
{
    public UntagProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.TagId)
            .NotEmpty();
    }
}

public sealed class UntagProductCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<UntagProductCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UntagProductCommand>
{
    private const string AppRequestName = nameof(UntagProductCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UntagProductCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UntagProductCommand request, CancellationToken cancellationToken)
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

            // Removing a tag the product never carried succeeds without an event, so no existence
            // check on the tag itself is needed.
            var untagResult = product.Untag(
                request.TagId,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (untagResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is
                // touched, so there is nothing to roll back.
                product.ClearDomainEvents();

                _logger.LogError("Unable to untag Product {ProductId}. Error message: {Error}", request.Id, untagResult.Error);
                return Result.Failure(untagResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} untagged from {TagId}.", request.Id, request.TagId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
