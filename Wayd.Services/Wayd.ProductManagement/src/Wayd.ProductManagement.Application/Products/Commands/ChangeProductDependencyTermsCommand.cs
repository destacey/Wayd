using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Extensions;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Changes the terms a product's dependency holds on — whether the product stops working without the one it
/// depends on, how it reaches it, or both.
/// </summary>
/// <remarks>
/// Ends the link and opens another, so the result is the id of the link now open — which differs from
/// <see cref="DependencyId"/> whenever the terms actually changed. Recording styles on a link that had none
/// is the exception: it fills them in place and returns the same id, since nothing about the dependency
/// changed.
/// </remarks>
/// <param name="InteractionStyles">
/// Null or empty carries the recorded styles onto the new link rather than clearing them.
/// </param>
/// <param name="ChangedOn">
/// The first day the new terms hold; the current link ends the day before. Defaults to today.
/// </param>
public sealed record ChangeProductDependencyTermsCommand(
    Guid Id,
    Guid DependencyId,
    DependencyStrength Strength,
    IReadOnlyCollection<InteractionStyle>? InteractionStyles,
    LocalDate? ChangedOn) : ICommand<Guid>;

public sealed class ChangeProductDependencyTermsCommandValidator : AbstractValidator<ChangeProductDependencyTermsCommand>
{
    public ChangeProductDependencyTermsCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.DependencyId)
            .NotEmpty();

        RuleFor(x => x.Strength)
            .IsInEnum();

        RuleForEach(x => x.InteractionStyles)
            .Must(Enum.IsDefined)
            .WithMessage("'{PropertyValue}' is not an interaction style.");
    }
}

public sealed class ChangeProductDependencyTermsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<ChangeProductDependencyTermsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeProductDependencyTermsCommand, Guid>
{
    private const string AppRequestName = nameof(ChangeProductDependencyTermsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ChangeProductDependencyTermsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(ChangeProductDependencyTermsCommand request, CancellationToken cancellationToken)
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

            var changeResult = product.ChangeDependencyTerms(
                request.DependencyId,
                request.Strength,
                request.InteractionStyles.ToFlagCombination(),
                request.ChangedOn ?? today,
                today,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (changeResult.IsFailure)
            {
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to change the terms of dependency {DependencyId} on Product {ProductId}. Error message: {Error}", request.DependencyId, request.Id, changeResult.Error);
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
