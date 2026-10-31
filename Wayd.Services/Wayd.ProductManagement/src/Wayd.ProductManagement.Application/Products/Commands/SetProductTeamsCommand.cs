namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Makes <paramref name="TeamIds"/> exactly the teams linked to a product: links the ones not yet on it and
/// unlinks the rest.
/// </summary>
/// <remarks>
/// Only the product's own links; teams linked to the products beneath it are not touched. All or nothing.
/// </remarks>
/// <param name="TeamIds">The teams and teams of teams that work on the product. Empty unlinks them all.</param>
public sealed record SetProductTeamsCommand(Guid Id, IReadOnlyCollection<Guid> TeamIds) : ICommand;

public sealed class SetProductTeamsCommandValidator : AbstractValidator<SetProductTeamsCommand>
{
    /// <summary>The most teams a product may be set to in one request.</summary>
    public const int MaxTeams = 100;

    public SetProductTeamsCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.TeamIds)
            .NotNull()
            .Must(ids => ids.Count <= MaxTeams).WithMessage($"A product can be set to at most {MaxTeams} teams at once.");

        RuleForEach(x => x.TeamIds)
            .NotEmpty();
    }
}

public sealed class SetProductTeamsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    IDispatcher dispatcher,
    ILogger<SetProductTeamsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetProductTeamsCommand>
{
    private const string AppRequestName = nameof(SetProductTeamsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<SetProductTeamsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ProductTeamCopies _teams = new(productManagementDbContext, dispatcher, dateTimeProvider, logger);

    public async Task<Result> Handle(SetProductTeamsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productManagementDbContext.Products
                .Include(p => p.TeamLinks)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.Id);
                return Result.Failure("Product not found.");
            }

            var wanted = request.TeamIds.ToHashSet();
            var actor = EventActor.User(_currentUser.GetUserId());
            var now = _dateTimeProvider.Now;

            foreach (var teamId in product.TeamLinks.Select(l => l.TeamId).Where(id => !wanted.Contains(id)).ToList())
            {
                product.UnlinkTeam(teamId, actor, now);
            }

            var linked = product.TeamLinks.Select(l => l.TeamId).ToHashSet();
            var toLink = wanted.Where(id => !linked.Contains(id)).ToList();

            foreach (var teamId in toLink)
            {
                var team = await _teams.Find(teamId, cancellationToken);
                if (team is null)
                {
                    product.ClearDomainEvents();

                    _logger.LogInformation("Team {TeamId} not found.", teamId);
                    return Result.Failure("Team not found.");
                }

                var linkResult = product.LinkTeam(team, actor, now);
                if (linkResult.IsFailure)
                {
                    // The changes so far are only in memory; the request ends without saving them.
                    product.ClearDomainEvents();

                    _logger.LogInformation("Unable to link Team {TeamId} to Product {ProductId}. Error message: {Error}", teamId, request.Id, linkResult.Error);
                    return Result.Failure(toLink.Count == 1 ? linkResult.Error : $"{team.Name}: {linkResult.Error}");
                }
            }

            try
            {
                await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Another request linked one of these teams while this one ran, and the unique index rejected
                // the duplicate. Nothing was saved, so the caller can simply try again.
                _logger.LogWarning(ex, "The teams of Product {ProductId} changed while they were being set.", request.Id);
                return Result.Failure("The product's teams were changed by someone else at the same time. Reload them and try again.");
            }

            _logger.LogInformation("Product {ProductId} set to {TeamCount} team(s).", request.Id, wanted.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
