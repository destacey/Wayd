namespace Wayd.ProductManagement.Application.Products.Commands;

public sealed record ReparentProductCommand(Guid Id, Guid? ParentId) : ICommand;

public sealed class ReparentProductCommandValidator : AbstractValidator<ReparentProductCommand>
{
    public ReparentProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.ParentId)
            .NotEqual(x => x.Id)
            .WithMessage("A product cannot be its own parent.");
    }
}

public sealed class ReparentProductCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<ReparentProductCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReparentProductCommand>
{
    private const string AppRequestName = nameof(ReparentProductCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ReparentProductCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ReparentProductCommand request, CancellationToken cancellationToken)
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

            var ancestorIds = Array.Empty<Guid>() as IReadOnlyCollection<Guid>;
            var dependsAcrossNewLineage = false;

            if (request.ParentId is not null)
            {
                if (!await _productManagementDbContext.Products.AnyAsync(p => p.Id == request.ParentId, cancellationToken))
                {
                    _logger.LogInformation("Parent Product {ParentId} not found.", request.ParentId);
                    return Result.Failure("Parent product not found.");
                }

                // Reparent's cycle check is only as good as what it is handed — an empty collection
                // for a real parent disables it silently — so this must walk the whole chain.
                var chain = await _productManagementDbContext.SelfAndAncestors(request.ParentId.Value, cancellationToken);
                if (chain.IsFailure)
                {
                    _logger.LogError("Unable to reparent Product {ProductId}. Error message: {Error}", request.Id, chain.Error);
                    return Result.Failure(chain.Error);
                }

                ancestorIds = chain.Value;
                dependsAcrossNewLineage = await DependsAcross(product.Id, ancestorIds, cancellationToken);
            }

            var reparentResult = product.Reparent(
                request.ParentId,
                ancestorIds,
                dependsAcrossNewLineage,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (reparentResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is
                // touched, so there is nothing to roll back.
                product.ClearDomainEvents();

                _logger.LogError("Unable to reparent Product {ProductId}. Error message: {Error}", request.Id, reparentResult.Error);
                return Result.Failure(reparentResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} reparented to {ParentId}.", request.Id, request.ParentId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    /// <summary>
    /// Whether an open dependency links the moved node or anything beneath it with the new lineage, either way.
    /// </summary>
    /// <remarks>
    /// Descendants count: moving a platform under a product that one of its services depends on makes that
    /// link composition just as surely as moving the service itself.
    /// </remarks>
    private async Task<bool> DependsAcross(Guid productId, IReadOnlyCollection<Guid> newLineage, CancellationToken cancellationToken)
    {
        var childrenByParent = (await _productManagementDbContext.Products
                .Where(p => p.ParentId != null)
                .Select(p => new { p.Id, ParentId = p.ParentId!.Value })
                .ToListAsync(cancellationToken))
            .ToLookup(p => p.ParentId, p => p.Id);

        var subtree = new HashSet<Guid> { productId };
        var pending = new Queue<Guid>([productId]);
        while (pending.TryDequeue(out var id))
        {
            foreach (var child in childrenByParent[id].Where(subtree.Add))
            {
                pending.Enqueue(child);
            }
        }

        return await _productManagementDbContext.ProductDependencies
            .Where(d => d.Period.End == null)
            .AnyAsync(d =>
                (subtree.Contains(d.ProductId) && newLineage.Contains(d.DependsOnProductId))
                || (newLineage.Contains(d.ProductId) && subtree.Contains(d.DependsOnProductId)),
                cancellationToken);
    }
}
