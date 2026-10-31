namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Makes <paramref name="ProductIds"/> exactly the products a team or team of teams is linked to: links the ones
/// it is not on and unlinks it from the rest.
/// </summary>
/// <remarks>
/// All or nothing, across every product it touches. Each product still records its own linking or unlinking,
/// since the links belong to the products.
/// </remarks>
/// <param name="ProductIds">The products the team works on. Empty unlinks it from every product.</param>
public sealed record SetTeamProductsCommand(Guid TeamId, IReadOnlyCollection<Guid> ProductIds) : ICommand;

public sealed class SetTeamProductsCommandValidator : AbstractValidator<SetTeamProductsCommand>
{
    /// <summary>The most products a team may be set to in one request.</summary>
    public const int MaxProducts = 500;

    public SetTeamProductsCommandValidator()
    {
        RuleFor(x => x.TeamId)
            .NotEmpty();

        RuleFor(x => x.ProductIds)
            .NotNull()
            .Must(ids => ids.Count <= MaxProducts).WithMessage($"A team can be set to at most {MaxProducts} products at once.");

        RuleForEach(x => x.ProductIds)
            .NotEmpty();
    }
}

public sealed class SetTeamProductsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    IDispatcher dispatcher,
    ILogger<SetTeamProductsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetTeamProductsCommand>
{
    private const string AppRequestName = nameof(SetTeamProductsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<SetTeamProductsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ProductTeamCopies _teams = new(productManagementDbContext, dispatcher, dateTimeProvider, logger);

    public async Task<Result> Handle(SetTeamProductsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var wanted = request.ProductIds.ToHashSet();
            var teamId = request.TeamId;

            // Every product the change touches: those asked for, and those the team is on now.
            var products = await _productManagementDbContext.Products
                .Include(p => p.TeamLinks)
                .Where(p => wanted.Contains(p.Id) || p.TeamLinks.Any(l => l.TeamId == teamId))
                .ToListAsync(cancellationToken);

            if (wanted.Except(products.Select(p => p.Id)).Any())
            {
                _logger.LogInformation("Unable to set the products of Team {TeamId}: a product was not found.", teamId);
                return Result.Failure("Product not found.");
            }

            // Only linking needs the team itself. A team being unlinked from everything may have no copy at all.
            ProductTeam? team = null;
            if (wanted.Count > 0)
            {
                team = await _teams.Find(teamId, cancellationToken);
                if (team is null)
                {
                    _logger.LogInformation("Team {TeamId} not found.", teamId);
                    return Result.Failure("Team not found.");
                }
            }

            var actor = EventActor.User(_currentUser.GetUserId());
            var now = _dateTimeProvider.Now;

            foreach (var product in products)
            {
                var result = wanted.Contains(product.Id)
                    ? product.LinkTeam(team!, actor, now)
                    : product.UnlinkTeam(teamId, actor, now);

                if (result.IsFailure)
                {
                    // The changes so far are only in memory; the request ends without saving them.
                    foreach (var changed in products)
                    {
                        changed.ClearDomainEvents();
                    }

                    _logger.LogInformation("Unable to set the products of Team {TeamId}. Error message: {Error}", teamId, result.Error);
                    return Result.Failure(result.Error);
                }
            }

            try
            {
                await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Another request linked the team to one of these products while this one ran, and the unique
                // index rejected the duplicate. Nothing was saved, so the caller can simply try again.
                _logger.LogWarning(ex, "The products of Team {TeamId} changed while they were being set.", teamId);
                return Result.Failure("The team's products were changed by someone else at the same time. Reload them and try again.");
            }

            _logger.LogInformation("Team {TeamId} set to {ProductCount} product(s).", teamId, wanted.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
