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

            if (request.ParentId is not null)
            {
                if (!await _productManagementDbContext.Products.AnyAsync(p => p.Id == request.ParentId, cancellationToken))
                {
                    _logger.LogInformation("Parent Product {ParentId} not found.", request.ParentId);
                    return Result.Failure("Parent product not found.");
                }

                // Reparent's cycle check is only as good as what it is handed — an empty collection
                // for a real parent disables it silently — so this must walk the whole chain.
                var chain = await AncestorsOf(request.ParentId.Value, cancellationToken);
                if (chain.IsFailure)
                {
                    return Result.Failure(chain.Error);
                }

                ancestorIds = chain.Value;
            }

            var reparentResult = product.Reparent(
                request.ParentId,
                ancestorIds,
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
    /// Walks from a node to the root, nearest ancestor first.
    /// </summary>
    /// <remarks>
    /// Iterative rather than a recursive CTE because the tree is small and this stays provider-agnostic.
    /// The visited set bounds it even if existing data already holds a cycle.
    /// </remarks>
    private async Task<Result<IReadOnlyCollection<Guid>>> AncestorsOf(Guid startId, CancellationToken cancellationToken)
    {
        var ancestors = new List<Guid>();
        var visited = new HashSet<Guid>();
        Guid? currentId = startId;

        while (currentId is not null)
        {
            if (!visited.Add(currentId.Value))
            {
                _logger.LogError("Product ancestry contains a cycle at {ProductId}.", currentId);
                return Result.Failure<IReadOnlyCollection<Guid>>("The product hierarchy contains a cycle and must be corrected first.");
            }

            ancestors.Add(currentId.Value);

            // Projected into a wrapper so a root (null ParentId) is distinguishable from a missing
            // row — both come back as default from a bare Guid? projection.
            var nodeId = currentId.Value;
            var parent = await _productManagementDbContext.Products
                .Where(p => p.Id == nodeId)
                .Select(p => new { p.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            currentId = parent?.ParentId;
        }

        return Result.Success<IReadOnlyCollection<Guid>>(ancestors);
    }
}
