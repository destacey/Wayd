namespace Wayd.Organization.Application.Teams.Commands;

public sealed record SyncTeamMembershipEdgesCommand() : ICommand, ILongRunningRequest;

public sealed class SyncTeamMembershipEdgesCommandHandler(IOrganizationDbContext organizationDbContext, ILogger<SyncTeamMembershipEdgesCommandHandler> logger) : ICommandHandler<SyncTeamMembershipEdgesCommand>
{
    private const string RequestName = nameof(SyncTeamMembershipEdgesCommand);
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<SyncTeamMembershipEdgesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncTeamMembershipEdgesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await _organizationDbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var deleteCount = await DeleteTeamMemberships(cancellationToken);
                _logger.LogInformation("{RequestName}: Deleted {DeleteCount} memberships", RequestName, deleteCount);

                var updateCount = await UpdateTeamMemberships(cancellationToken);
                _logger.LogInformation("{RequestName}: Updated {UpdateCount} memberships", RequestName, updateCount);

                var insertCount = await InsertNewTeamMemberships(cancellationToken);
                _logger.LogInformation("{RequestName}: Inserted {InsertCount} new memberships", RequestName, insertCount);

                await transaction.CommitAsync(cancellationToken);

                return Result.Success();
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);

            return Result.Failure<int>($"Exception for request {RequestName} {request}");
        }
    }

    /// <summary>
    /// Insert new team memberships that don't exist in TeamMembershipEdges
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of rows affected.</returns>
    private async Task<int> InsertNewTeamMemberships(CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [Organization].[TeamMembershipEdges] 
                ([Id], [StartDate], [EndDate], $from_id, $to_id)
            SELECT 
                tm.Id, 
                CAST(tm.[Start] AS DATETIME2(7)) AS StartDate,  
                CASE 
                    WHEN tm.[End] IS NULL THEN NULL
                    ELSE CAST(tm.[End] AS DATETIME2(7)) 
                END AS EndDate,
                (SELECT $node_id FROM [Organization].[TeamNodes] WHERE Id = tm.SourceId) AS FromNode,
                (SELECT $node_id FROM [Organization].[TeamNodes] WHERE Id = tm.TargetId) AS ToNode
            FROM 
                [Organization].[TeamMemberships] tm
            WHERE 
                tm.IsDeleted = 0 -- Only consider active records
                AND NOT EXISTS (
                    SELECT 1 
                    FROM [Organization].[TeamMembershipEdges] tme
                    WHERE 
                        tme.Id = tm.Id -- Check if the edge already exists
                );";

        return await _organizationDbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    /// <summary>
    /// Update team memberships that have changed
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of rows affected.</returns>
    private async Task<int> UpdateTeamMemberships(CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE tme
            SET 
                tme.[StartDate] = CAST(tm.[Start] AS DATETIME2(7)),
                tme.[EndDate] = CASE 
                                    WHEN tm.[End] IS NULL THEN NULL
                                    ELSE CAST(tm.[End] AS DATETIME2(7))
                                END
            FROM 
                [Organization].[TeamMembershipEdges] tme
            INNER JOIN 
                [Organization].[TeamMemberships] tm
            ON 
                tme.Id = tm.Id
            WHERE 
                tm.IsDeleted = 0 -- Only consider active records
                AND (
                    tme.[StartDate] <> CAST(tm.[Start] AS DATETIME2(7)) OR
                    (tme.[EndDate] IS NULL AND tm.[End] IS NOT NULL) OR
                    (tme.[EndDate] IS NOT NULL AND tm.[End] IS NULL) OR
                    tme.[EndDate] <> CAST(tm.[End] AS DATETIME2(7))
                );";

        return await _organizationDbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    /// <summary>
    /// Delete team membersships that no longer exist
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of rows affected.</returns>
    private async Task<int> DeleteTeamMemberships(CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE e
            FROM [Organization].[TeamMembershipEdges] e
                LEFT JOIN [Organization].[TeamMemberships] m ON e.[Id] = m.[Id]
            WHERE m.[Id] IS NULL 
                OR m.[IsDeleted] = 1
                OR NOT EXISTS (SELECT 1 FROM [Organization].[TeamNodes] WHERE [Id] = m.[SourceId])
                OR NOT EXISTS (SELECT 1 FROM [Organization].[TeamNodes] WHERE [Id] = m.[TargetId]);";

        return await _organizationDbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}

