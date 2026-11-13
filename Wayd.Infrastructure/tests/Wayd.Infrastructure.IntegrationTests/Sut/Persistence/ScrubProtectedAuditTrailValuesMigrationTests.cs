using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.Infrastructure.Auditing;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Scrub-Protected-AuditTrail-Values migration removes a connection's configuration, a time-away
/// reason and a token hash from existing trail rows, under every key and casing they were written with, and leaves
/// everything else.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ScrubProtectedAuditTrailValuesMigrationTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Scrub_RemovesProtectedValuesUnderEitherCasing_AndKeepsTheRest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var key = Guid.NewGuid().ToString();
        var camelConnection = Row("AzureOpenAIConnection", key,
            old: null,
            @new: """{"name":"AI","configuration":{"ApiKey":"plain-1","DeploymentName":"gpt-4o"}}""");
        var pascalConnection = Row("AzdoBoardsConnection", key,
            old: """{"$id":"1","Configuration":{"$id":"2","PersonalAccessToken":"plain-2"}}""",
            @new: """{"$id":"1","Configuration":{"$id":"2","PersonalAccessToken":"plain-3"},"Name":"Boards"}""");
        var legacyConnection = Row("AzureDevOpsBoardsConnector", key,
            old: null,
            @new: """{"Name":"Boards","ConfigurationString":"{\"PersonalAccessToken\":\"plain-4\"}"}""");
        var reasonOnly = Row("EmployeeTimeAway", key,
            old: """{"reason":"paidTimeOff"}""",
            @new: """{"reason":"businessTravel"}""",
            affectedColumns: """["Reason"]""");
        var token = Row("PersonalAccessToken", key,
            old: null,
            @new: """{"name":"Build agent","tokenHash":"hash-1"}""");
        var untouched = Row("Project", key,
            old: null,
            @new: """{"name":"Apollo","configuration":{"kept":true},"reason":"kept"}""");
        var notJson = Row("WorkdayConnection", key,
            old: null,
            @new: "not json");

        await using (var context = _fixture.CreateContext())
        {
            context.AuditTrails.AddRange(camelConnection, pascalConnection, legacyConnection, reasonOnly, token, untouched, notJson);
            await context.SaveChangesAsync(ct);
        }

        // Act
        await RunScrub(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var rows = await verify.AuditTrails.AsNoTracking()
            .Where(t => t.PrimaryKey == key)
            .ToDictionaryAsync(t => t.Id, ct);

        Json(rows[camelConnection.Id].NewValues).Should().ContainKey("name").And.NotContainKey("configuration");

        Json(rows[pascalConnection.Id].OldValues).Should().NotContainKey("Configuration");
        Json(rows[pascalConnection.Id].NewValues).Should().ContainKey("Name").And.NotContainKey("Configuration");

        Json(rows[legacyConnection.Id].NewValues).Should().ContainKey("Name").And.NotContainKey("ConfigurationString");

        rows[reasonOnly.Id].OldValues.Should().BeNull("a payload left empty is null, as the writer leaves it");
        rows[reasonOnly.Id].NewValues.Should().BeNull();
        rows[reasonOnly.Id].AffectedColumns.Should().Be("""["Reason"]""", "the row still records that the reason changed");

        Json(rows[token.Id].NewValues).Should().ContainKey("name").And.NotContainKey("tokenHash");

        rows[untouched.Id].NewValues.Should().Be(untouched.NewValues, "only the three affected tables are scrubbed");
        rows[notJson.Id].NewValues.Should().Be("not json", "a row that is not JSON is left alone rather than failing the migration");
    }

    private static Trail Row(string tableName, string primaryKey, string? old, string @new, string? affectedColumns = null) => new()
    {
        UserId = "migration-test",
        Type = old is null ? nameof(TrailType.Create) : nameof(TrailType.Update),
        TableName = tableName,
        DateTime = Instant.FromUtc(2026, 1, 1, 0, 0),
        PrimaryKey = primaryKey,
        OldValues = old,
        NewValues = @new,
        AffectedColumns = affectedColumns,
    };

    private static JsonObject Json(string? payload) => JsonNode.Parse(payload!)!.AsObject();

    // Straight through ADO: the empty-object literal's braces would be read as format placeholders by ExecuteSqlRaw.
    private async Task RunScrub(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = ScrubProtectedAuditTrailValues.ScrubSql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
