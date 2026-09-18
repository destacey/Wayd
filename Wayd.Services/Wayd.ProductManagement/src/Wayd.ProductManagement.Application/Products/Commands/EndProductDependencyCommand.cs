namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Records that a product stopped depending on another. The link is kept.
/// </summary>
/// <param name="EndsOn">The last day the dependency held. Defaults to today.</param>
public sealed record EndProductDependencyCommand(Guid Id, Guid DependencyId, LocalDate? EndsOn) : ICommand;

public sealed class EndProductDependencyCommandValidator : AbstractValidator<EndProductDependencyCommand>
{
    public EndProductDependencyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.DependencyId)
            .NotEmpty();
    }
}

public sealed class EndProductDependencyCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<EndProductDependencyCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<EndProductDependencyCommand>
{
    private const string AppRequestName = nameof(EndProductDependencyCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<EndProductDependencyCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(EndProductDependencyCommand request, CancellationToken cancellationToken)
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

            var today = _dateTimeProvider.Today;

            var endResult = product.EndDependency(
                request.DependencyId,
                request.EndsOn ?? today,
                today,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (endResult.IsFailure)
            {
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to end dependency {DependencyId} on Product {ProductId}. Error message: {Error}", request.DependencyId, request.Id, endResult.Error);
                return Result.Failure(endResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Dependency {DependencyId} on Product {ProductId} ended.", request.DependencyId, request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
