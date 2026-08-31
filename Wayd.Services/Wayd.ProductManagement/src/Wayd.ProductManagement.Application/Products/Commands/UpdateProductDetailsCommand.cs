namespace Wayd.ProductManagement.Application.Products.Commands;

public sealed record UpdateProductDetailsCommand(
    Guid Id,
    string Name,
    string? Description) : ICommand;

public sealed class UpdateProductDetailsCommandValidator : AbstractValidator<UpdateProductDetailsCommand>
{
    public UpdateProductDetailsCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}

public sealed class UpdateProductDetailsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<UpdateProductDetailsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateProductDetailsCommand>
{
    private const string AppRequestName = nameof(UpdateProductDetailsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateProductDetailsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateProductDetailsCommand request, CancellationToken cancellationToken)
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

            var updateResult = product.UpdateDetails(
                request.Name,
                request.Description,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (updateResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is
                // touched, so there is nothing to roll back.
                product.ClearDomainEvents();

                _logger.LogError("Unable to update Product {ProductId}. Error message: {Error}", request.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
