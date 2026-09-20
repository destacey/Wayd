using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Extensions;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Records that a product depends on another.
/// </summary>
/// <param name="InteractionStyles">
/// How the product reaches the one it depends on, several at once where it both calls and subscribes. Null
/// or empty records none, which is not the same as recording that there are none.
/// </param>
/// <param name="StartsOn">The day the dependency began. Defaults to today, and may be backdated.</param>
public sealed record AddProductDependencyCommand(
    Guid Id,
    Guid DependsOnProductId,
    DependencyStrength Strength,
    IReadOnlyCollection<InteractionStyle>? InteractionStyles,
    string? Description,
    LocalDate? StartsOn) : ICommand<Guid>;

public sealed class AddProductDependencyCommandValidator : AbstractValidator<AddProductDependencyCommand>
{
    public AddProductDependencyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.DependsOnProductId)
            .NotEmpty()
            .NotEqual(x => x.Id)
            .WithMessage("A product cannot depend on itself.");

        RuleFor(x => x.Strength)
            .IsInEnum();

        // Each entry names one style. IsInEnum would accept a combination, since on a flags enum it tests
        // the bits rather than the declared members.
        RuleForEach(x => x.InteractionStyles)
            .Must(Enum.IsDefined)
            .WithMessage("'{PropertyValue}' is not an interaction style.");

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}

public sealed class AddProductDependencyCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<AddProductDependencyCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AddProductDependencyCommand, Guid>
{
    private const string AppRequestName = nameof(AddProductDependencyCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<AddProductDependencyCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(AddProductDependencyCommand request, CancellationToken cancellationToken)
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

            if (!await _productManagementDbContext.Products.AnyAsync(p => p.Id == request.DependsOnProductId, cancellationToken))
            {
                _logger.LogInformation("Product {DependsOnProductId} not found.", request.DependsOnProductId);
                return Result.Failure<Guid>("The product depended on was not found.");
            }

            // The composition check is only as good as the two ancestries — empty ones silently let a product
            // depend on its own parent or child — so both chains are walked in full.
            var ancestors = await _productManagementDbContext.SelfAndAncestors(product.Id, cancellationToken);
            if (ancestors.IsFailure)
            {
                return Result.Failure<Guid>(ancestors.Error);
            }

            var dependsOnAncestors = await _productManagementDbContext.SelfAndAncestors(request.DependsOnProductId, cancellationToken);
            if (dependsOnAncestors.IsFailure)
            {
                return Result.Failure<Guid>(dependsOnAncestors.Error);
            }

            var today = _dateTimeProvider.Today;

            var addResult = product.AddDependency(
                request.DependsOnProductId,
                request.Strength,
                request.InteractionStyles.ToFlagCombination(),
                request.Description,
                request.StartsOn ?? today,
                ancestors.Value,
                dependsOnAncestors.Value,
                today,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (addResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is
                // touched, so there is nothing to roll back.
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to add a dependency to Product {ProductId}. Error message: {Error}", request.Id, addResult.Error);
                return Result.Failure<Guid>(addResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} now depends on {DependsOnProductId}.", request.Id, request.DependsOnProductId);

            return Result.Success(addResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
