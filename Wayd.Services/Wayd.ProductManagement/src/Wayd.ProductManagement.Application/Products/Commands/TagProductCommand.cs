namespace Wayd.ProductManagement.Application.Products.Commands;

public sealed record TagProductCommand(Guid Id, Guid TagId) : ICommand;

public sealed class TagProductCommandValidator : AbstractValidator<TagProductCommand>
{
    public TagProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.TagId)
            .NotEmpty();
    }
}

public sealed class TagProductCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<TagProductCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<TagProductCommand>
{
    private const string AppRequestName = nameof(TagProductCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<TagProductCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(TagProductCommand request, CancellationToken cancellationToken)
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

            var tag = await _productManagementDbContext.ProductTags
                .FirstOrDefaultAsync(t => t.Id == request.TagId, cancellationToken);

            if (tag is null)
            {
                _logger.LogInformation("Product Tag {TagId} not found.", request.TagId);
                return Result.Failure("Tag not found.");
            }

            // The axis decides whether this replaces an existing tag or joins it, so the aggregate
            // needs it loaded alongside the tag.
            var category = await _productManagementDbContext.ProductTagCategories
                .FirstOrDefaultAsync(c => c.Id == tag.CategoryId, cancellationToken);

            if (category is null)
            {
                _logger.LogError("Tag {TagId} references missing category {CategoryId}.", request.TagId, tag.CategoryId);
                return Result.Failure("That tag's category no longer exists.");
            }

            if (!category.IsActive)
            {
                return Result.Failure($"'{category.Name}' is inactive and cannot be used to tag products.");
            }

            var tagResult = product.Tag(
                tag,
                category,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (tagResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is
                // touched, so there is nothing to roll back.
                product.ClearDomainEvents();

                _logger.LogError("Unable to tag Product {ProductId}. Error message: {Error}", request.Id, tagResult.Error);
                return Result.Failure(tagResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} tagged with {TagId}.", request.Id, request.TagId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
