using Wayd.Common.Application.Requests.Organization;

namespace Wayd.ProductManagement.Application.Products;

/// <summary>
/// Finds Product Management's copy of a team for a link, building it from Organization when it has none yet.
/// </summary>
/// <remarks>
/// A team created moments ago, or every team before the first sync, has no copy; refusing to link it until the
/// background job ran would be a failure nobody could explain.
/// </remarks>
internal sealed class ProductTeamCopies(
    IProductManagementDbContext productManagementDbContext,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider,
    ILogger logger)
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger _logger = logger;

    /// <summary>
    /// The team's copy, built and saved when there is none, or <c>null</c> when Organization has no such team.
    /// </summary>
    public async Task<ProductTeam?> Find(Guid teamId, CancellationToken cancellationToken) =>
        await _productManagementDbContext.ProductTeams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken)
        ?? await CopyFromSource(teamId, cancellationToken);

    /// <summary>
    /// Builds and saves the module's copy of a team that has none yet, as the <c>Team*</c> event handler would.
    /// </summary>
    /// <remarks>
    /// Stamped with the time taken before the read, as the sync is: a change committed after the read then
    /// arrives as newer than the copy and is applied, rather than skipped as stale.
    /// <para>
    /// Saved on its own, before any product is touched, because the <c>TeamCreated</c> handler may be inserting
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
    /// Whether a failed insert of <paramref name="team"/> lost to another writer's copy of it. Stops tracking the
    /// rejected copy either way, so it is not inserted again with the links.
    /// </summary>
    private async Task<bool> CopyWasSavedElsewhere(ProductTeam team, CancellationToken cancellationToken)
    {
        _productManagementDbContext.Entry(team).State = EntityState.Detached;

        return await _productManagementDbContext.ProductTeams
            .AsNoTracking()
            .AnyAsync(t => t.Id == team.Id, cancellationToken);
    }
}
