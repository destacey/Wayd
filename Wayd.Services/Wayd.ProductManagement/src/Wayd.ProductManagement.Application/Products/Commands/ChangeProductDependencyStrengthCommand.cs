using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Changes whether a product stops working without one it depends on.
/// </summary>
/// <remarks>
/// Ends the link and opens another, so the result is the id of the link now open — which differs from
/// <see cref="DependencyId"/> whenever the strength actually changed.
/// </remarks>
/// <param name="ChangedOn">
/// The first day the new strength holds; the current link ends the day before. Defaults to today.
/// </param>
public sealed record ChangeProductDependencyStrengthCommand(
    Guid Id,
    Guid DependencyId,
    DependencyStrength Strength,
    LocalDate? ChangedOn) : ICommand<Guid>;

public sealed class ChangeProductDependencyStrengthCommandValidator : AbstractValidator<ChangeProductDependencyStrengthCommand>
{
    public ChangeProductDependencyStrengthCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.DependencyId)
            .NotEmpty();

        RuleFor(x => x.Strength)
            .IsInEnum();
    }
}

public sealed class ChangeProductDependencyStrengthCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<ChangeProductDependencyStrengthCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeProductDependencyStrengthCommand, Guid>
{
    private const string AppRequestName = nameof(ChangeProductDependencyStrengthCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ChangeProductDependencyStrengthCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(ChangeProductDependencyStrengthCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productManagementDbContext.Products
                .Include(p => p.Dependencies)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.Id);
                return Result.Failure<Guid>("Product not found.");
            }

            var today = _dateTimeProvider.Today;

            var changeResult = product.ChangeDependencyStrength(
                request.DependencyId,
                request.Strength,
                request.ChangedOn ?? today,
                today,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (changeResult.IsFailure)
            {
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to change the strength of dependency {DependencyId} on Product {ProductId}. Error message: {Error}", request.DependencyId, request.Id, changeResult.Error);
                return Result.Failure<Guid>(changeResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Dependency {DependencyId} on Product {ProductId} is now {Strength}.", request.DependencyId, request.Id, request.Strength);

            return Result.Success(changeResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
