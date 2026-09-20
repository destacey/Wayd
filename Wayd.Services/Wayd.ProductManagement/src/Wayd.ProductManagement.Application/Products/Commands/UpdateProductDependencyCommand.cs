using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Extensions;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Rewords what a product's dependency is for, and records the styles it uses where none were recorded.
/// </summary>
/// <param name="InteractionStyles">
/// Null or empty leaves recorded styles alone, where a null <paramref name="Description"/> clears it.
/// Changing styles already recorded is refused here — it is a change of terms, which has to be dated.
/// </param>
public sealed record UpdateProductDependencyCommand(
    Guid Id,
    Guid DependencyId,
    string? Description,
    IReadOnlyCollection<InteractionStyle>? InteractionStyles) : ICommand;

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

        RuleForEach(x => x.InteractionStyles)
            .Must(Enum.IsDefined)
            .WithMessage("'{PropertyValue}' is not an interaction style.");
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
                request.InteractionStyles.ToFlagCombination(),
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
