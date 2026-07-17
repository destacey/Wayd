namespace Wayd.Organization.Application.Teams.Commands;

public sealed record SyncTeamNodesCommand() : ICommand, ILongRunningRequest;

public sealed class SyncTeamNodesCommandHandler(IOrganizationDbContext organizationDbContext, ILogger<SyncTeamNodesCommandHandler> logger) : ICommandHandler<SyncTeamNodesCommand>
{
    private const string RequestName = nameof(SyncTeamNodesCommand);
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<SyncTeamNodesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncTeamNodesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await _organizationDbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var deleteCount = await DeleteTeams(cancellationToken);
                _logger.LogInformation("{RequestName}: Deleted {DeleteCount} teams", RequestName, deleteCount);

                var updateCount = await UpdateTeams(cancellationToken);
                _logger.LogInformation("{RequestName}: Updated {UpdateCount} teams", RequestName, updateCount);

                var insertCount = await InsertNewTeams(cancellationToken);
                _logger.LogInformation("{RequestName}: Inserted {InsertCount} new teams", RequestName, insertCount);

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
    /// Insert new teams that don't exist in TeamNodes
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of rows affected.</returns>
    private async Task<int> InsertNewTeams(CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [Organization].[TeamNodes] (
                [Id], [Key], [Name], [Code], [Type], [IsActive], [ActiveDate], [InactiveDate]
            )
            SELECT 
                t.[Id], t.[Key], t.[Name], t.[Code], t.[Type], t.[IsActive],
                CAST(t.[ActiveDate] as datetime2), 
                CASE 
                    WHEN t.[InactiveDate] IS NOT NULL THEN CAST(t.[InactiveDate] as datetime2)
                    ELSE NULL 
                END
            FROM [Organization].[Teams] t
                LEFT JOIN [Organization].[TeamNodes] tn ON t.[Id] = tn.[Id]
            WHERE tn.[Id] IS NULL
                AND t.[IsDeleted] = 0;";

        return await _organizationDbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    /// <summary>
    /// Update teams that have changed
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of rows affected.</returns>
    private async Task<int> UpdateTeams(CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE tn
            SET 
                tn.[Key] = t.[Key],
                tn.[Name] = t.[Name],
                tn.[Code] = t.[Code],
                tn.[Type] = t.[Type],
                tn.[IsActive] = t.[IsActive],
                tn.[ActiveDate] = CAST(t.[ActiveDate] as datetime2),
                tn.[InactiveDate] = CASE 
                    WHEN t.[InactiveDate] IS NOT NULL THEN CAST(t.[InactiveDate] as datetime2)
                    ELSE NULL 
                END
            FROM [Organization].[TeamNodes] tn
                INNER JOIN [Organization].[Teams] t ON tn.[Id] = t.[Id]
            WHERE t.[IsDeleted] = 0
                AND (
                    tn.[Key] != t.[Key]
                    OR tn.[Name] != t.[Name]
                    OR tn.[Code] != t.[Code]
                    OR tn.[Type] != t.[Type]
                    OR tn.[IsActive] != t.[IsActive]
                    OR CAST(tn.[ActiveDate] as date) != t.[ActiveDate]
                    OR (tn.[InactiveDate] IS NULL AND t.[InactiveDate] IS NOT NULL)
                    OR (tn.[InactiveDate] IS NOT NULL AND t.[InactiveDate] IS NULL)
                    OR (tn.[InactiveDate] IS NOT NULL AND t.[InactiveDate] IS NOT NULL 
                        AND CAST(tn.[InactiveDate] as date) != t.[InactiveDate])
                );";

        return await _organizationDbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    /// <summary>
    /// Delete teams that no longer exist
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of rows affected.</returns>
    private async Task<int> DeleteTeams(CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE tn
            FROM [Organization].[TeamNodes] tn
                LEFT JOIN [Organization].[Teams] t ON tn.[Id] = t.[Id]
            WHERE t.[Id] IS NULL OR t.[IsDeleted] = 1;";

        return await _organizationDbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}

