using Wayd.Common.Application.Requests.Organization;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Links a team or team of teams to a product. Succeeds, changing nothing, when it is already linked.
/// </summary>
public sealed record LinkProductTeamCommand(Guid Id, Guid TeamId) : ICommand;

public sealed class LinkProductTeamCommandValidator : AbstractValidator<LinkProductTeamCommand>
{
    public LinkProductTeamCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.TeamId)
            .NotEmpty();
    }
}

public sealed class LinkProductTeamCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    IDispatcher dispatcher,
    ILogger<LinkProductTeamCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<LinkProductTeamCommand>
{
    private const string AppRequestName = nameof(LinkProductTeamCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<LinkProductTeamCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(LinkProductTeamCommand request, CancellationToken cancellationToken)
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

            var team = await _productManagementDbContext.ProductTeams
                .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken)
                ?? await CopyFromSource(request.TeamId, cancellationToken);

            if (team is null)
            {
                _logger.LogInformation("Team {TeamId} not found.", request.TeamId);
                return Result.Failure("Team not found.");
            }

            var linkResult = product.LinkTeam(
                team,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (linkResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is touched.
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to link Team {TeamId} to Product {ProductId}. Error message: {Error}", request.TeamId, request.Id, linkResult.Error);
                return Result.Failure(linkResult.Error);
            }

            try
            {
                await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Two requests for the same link both pass the aggregate's check, and the unique index rejects
                // the second. The link the caller asked for exists, so that is the no-op success a repeat gets.
                if (!await LinkWasSavedElsewhere(request, cancellationToken))
                {
                    throw;
                }

                _logger.LogInformation("Team {TeamId} was linked to Product {ProductId} by another request first.", request.TeamId, request.Id);

                return Result.Success();
            }

            _logger.LogInformation("Team {TeamId} linked to Product {ProductId}.", request.TeamId, request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    /// <summary>
    /// Builds and saves the module's copy of a team that has none yet, as the <c>Team*</c> event handler would.
    /// </summary>
    /// <remarks>
    /// A team created moments ago, or every team before the first sync, has no copy; refusing to link it until
    /// the background job ran would be a failure nobody could explain.
    /// <para>
    /// Stamped with the time taken before the read, as the sync is: a change committed after the read then
    /// arrives as newer than the copy and is applied, rather than skipped as stale.
    /// </para>
    /// <para>
    /// Saved on its own, before the product is touched, because the <c>TeamCreated</c> handler may be inserting
    /// the same copy. If it wins, this save fails on the key with nothing else staged, and the copy it saved is
    /// read instead.
    /// </para>
    /// </remarks>
    private async Task<ProductTeam?> CopyFromSource(Guid teamId, CancellationToken cancellationToken)
    {
        var asOf = _dateTimeProvider.Now;

        var source = await _dispatcher.Send(new GetSimpleTeamQuery(teamId), cancellationToken);
        if (source is null)
        {
            return null;
        }

        var team = new ProductTeam(source, asOf);
        await _productManagementDbContext.ProductTeams.AddAsync(team, cancellationToken);

        try
        {
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            return team;
        }
        catch (DbUpdateException)
        {
            if (!await CopyWasSavedElsewhere(team, cancellationToken))
            {
                throw;
            }

            _logger.LogInformation("Team {TeamId} was copied by another writer first; using that copy.", teamId);

            return await _productManagementDbContext.ProductTeams
                .FirstAsync(t => t.Id == teamId, cancellationToken);
        }
    }

    /// <summary>
    /// Whether the link a failed save was inserting has been saved by another request.
    /// </summary>
    private Task<bool> LinkWasSavedElsewhere(LinkProductTeamCommand request, CancellationToken cancellationToken) =>
        _productManagementDbContext.ProductTeamLinks
            .AsNoTracking()
            .AnyAsync(l => l.ProductId == request.Id && l.TeamId == request.TeamId, cancellationToken);

    /// <summary>
    /// Whether a failed insert of <paramref name="team"/> lost to another writer's copy of it. Stops tracking the
    /// rejected copy either way, so it is not inserted again with the link.
    /// </summary>
    private async Task<bool> CopyWasSavedElsewhere(ProductTeam team, CancellationToken cancellationToken)
    {
        _productManagementDbContext.Entry(team).State = EntityState.Detached;

        return await _productManagementDbContext.ProductTeams
            .AsNoTracking()
            .AnyAsync(t => t.Id == team.Id, cancellationToken);
    }
}
