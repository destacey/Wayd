namespace Wayd.ProductManagement.Application.Products.Commands;

/// <param name="ExternalId">The identifier in the owning system, or <c>null</c> to unlink.</param>
public sealed record LinkProductExternallyCommand(Guid Id, string? ExternalId) : ICommand;

public sealed class LinkProductExternallyCommandValidator : AbstractValidator<LinkProductExternallyCommand>
{
    public LinkProductExternallyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.ExternalId)
            .MaximumLength(256);
    }
}

public sealed class LinkProductExternallyCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<LinkProductExternallyCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<LinkProductExternallyCommand>
{
    private const string AppRequestName = nameof(LinkProductExternallyCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<LinkProductExternallyCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(LinkProductExternallyCommand request, CancellationToken cancellationToken)
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

            var linkResult = product.LinkExternally(
                request.ExternalId,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (linkResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is
                // touched, so there is nothing to roll back.
                product.ClearDomainEvents();

                _logger.LogError("Unable to link Product {ProductId}. Error message: {Error}", request.Id, linkResult.Error);
                return Result.Failure(linkResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} external link set.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
