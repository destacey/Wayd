using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;
using Serilog;
using Serilog.Events;
using Wayd.Infrastructure.Logging;

namespace Wayd.Infrastructure.Tests.Sut.Logging;

/// <summary>
/// Runs <see cref="DbUpdateExceptionDestructurer"/> through the exception details enricher as the API
/// registers it (<see cref="ExceptionDetails.WithWaydExceptionDetails"/>).
/// </summary>
public sealed class DbUpdateExceptionDestructurerTests
{
    private const string Secret = "pat_s3cr3t-value";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Destructure_RecordsEntityTypeAndStateWithoutValues(bool concurrency)
    {
        // Arrange
        using var context = new SecretContext();
        var entry = context.Add(new SecretEntity { Id = 1, Token = Secret });
        // The concurrency exception takes only update entries; the change tracker's entry is one.
#pragma warning disable EF1001
        IReadOnlyList<IUpdateEntry> entries = [entry.GetInfrastructure()];
#pragma warning restore EF1001
        var exception = concurrency
            ? new DbUpdateConcurrencyException("save failed", entries)
            : new DbUpdateException("save failed", entries);
        var (logger, sink) = CreateLogger();

        // Act
        logger.Error(exception, "Save failed");

        // Assert
        var details = (DictionaryValue)sink.Events.Should().ContainSingle().Subject.Properties["ExceptionDetail"];
        var logged = (SequenceValue)details.Elements.Single(e => Equals(e.Key.Value, "Entries")).Value;
        var only = (DictionaryValue)logged.Elements.Should().ContainSingle().Subject;
        only.Elements.ToDictionary(e => e.Key.Value!.ToString()!, e => e.Value.ToString())
            .Should().BeEquivalentTo(new Dictionary<string, string>
            {
                ["EntityType"] = "\"SecretEntity\"",
                ["State"] = "\"Added\"",
            });
        details.ToString().Should().NotContain(Secret);
    }

    private static (ILogger Logger, CollectingSink Sink) CreateLogger()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .Enrich.WithWaydExceptionDetails()
            .WriteTo.Sink(sink)
            .CreateLogger();

        return (logger, sink);
    }

    private sealed class SecretEntity
    {
        public int Id { get; set; }

        public string Token { get; set; } = string.Empty;
    }

    private sealed class SecretContext : DbContext
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        public DbSet<SecretEntity> Secrets => Set<SecretEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInMemoryDatabase(_databaseName);
    }
}
