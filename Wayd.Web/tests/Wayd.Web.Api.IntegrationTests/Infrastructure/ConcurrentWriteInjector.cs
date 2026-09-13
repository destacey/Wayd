using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayd.Planning.Domain.Models;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Simulates a second writer landing between a handler's read and its save: when armed for a
/// <see cref="PlanningTeam"/> copy, the next save that modifies that copy first touches the row from a
/// separate connection, which moves its row version and makes the save fail with a concurrency conflict.
/// </summary>
/// <remarks>
/// Registered on the shared host for every test, so it does nothing until <see cref="Arm"/> is called, and
/// it fires once.
/// </remarks>
public sealed class ConcurrentWriteInjector(Func<string> connectionString) : SaveChangesInterceptor
{
    private readonly Func<string> _connectionString = connectionString;
    private Guid? _armedFor;
    private int _fired;

    public bool Fired => _fired > 0;

    public void Arm(Guid planningTeamId)
    {
        _fired = 0;
        _armedFor = planningTeamId;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_armedFor is { } id
            && eventData.Context is not null
            && eventData.Context.ChangeTracker.Entries<PlanningTeam>().Any(e => e.State == EntityState.Modified && e.Entity.Id == id)
            && Interlocked.CompareExchange(ref _fired, 1, 0) == 0)
        {
            _armedFor = null;

            await using var connection = new SqlConnection(_connectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE [Planning].[PlanningTeams] SET [Name] = [Name] WHERE [Id] = @id";
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return result;
    }
}
