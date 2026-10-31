namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Links teams or teams of teams to a product. A team already linked is left as it is.
/// </summary>
/// <remarks>All or nothing: when any team cannot be linked, none is.</remarks>
public sealed record LinkProductTeamsCommand(Guid Id, IReadOnlyCollection<Guid> TeamIds) : ICommand;

public sealed class LinkProductTeamsCommandValidator : AbstractValidator<LinkProductTeamsCommand>
{
    /// <summary>The most teams one request may link.</summary>
    public const int MaxTeams = 100;

    public LinkProductTeamsCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.TeamIds)
            .NotEmpty()
            .Must(ids => ids.Count <= MaxTeams).WithMessage($"At most {MaxTeams} teams can be linked at once.");

        RuleForEach(x => x.TeamIds)
            .NotEmpty();
    }
}

public sealed class LinkProductTeamsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    IDispatcher dispatcher,
    ILogger<LinkProductTeamsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<LinkProductTeamsCommand>
{
    private const string AppRequestName = nameof(LinkProductTeamsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<LinkProductTeamsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ProductTeamCopies _teams = new(productManagementDbContext, dispatcher, dateTimeProvider, logger);

    public async Task<Result> Handle(LinkProductTeamsCommand request, CancellationToken cancellationToken)
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

            var teamIds = request.TeamIds.Distinct().ToList();
            var actor = EventActor.User(_currentUser.GetUserId());
            var now = _dateTimeProvider.Now;

            foreach (var teamId in teamIds)
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
                    // Links added before this one are only in memory; the request ends without saving them.
                    product.ClearDomainEvents();

                    _logger.LogInformation("Unable to link Team {TeamId} to Product {ProductId}. Error message: {Error}", teamId, request.Id, linkResult.Error);
                    // With several teams, the refusal names the one it is about.
                    return Result.Failure(teamIds.Count == 1 ? linkResult.Error : $"{team.Name}: {linkResult.Error}");
                }
            }

            try
            {
                await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Two requests for the same link both pass the aggregate's check, and the unique index rejects
                // the second. When every link asked for now exists, that is the no-op success a repeat gets.
                if (!await LinksWereSavedElsewhere(request.Id, teamIds, cancellationToken))
                {
                    throw;
                }

                _logger.LogInformation("The teams were linked to Product {ProductId} by another request first.", request.Id);

                return Result.Success();
            }

            _logger.LogInformation("{TeamCount} team(s) linked to Product {ProductId}.", teamIds.Count, request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    /// <summary>
    /// Whether every link a failed save was inserting has been saved by another request.
    /// </summary>
    private async Task<bool> LinksWereSavedElsewhere(Guid productId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken) =>
        await _productManagementDbContext.ProductTeamLinks
            .AsNoTracking()
            .CountAsync(l => l.ProductId == productId && teamIds.Contains(l.TeamId), cancellationToken) == teamIds.Count;
}
