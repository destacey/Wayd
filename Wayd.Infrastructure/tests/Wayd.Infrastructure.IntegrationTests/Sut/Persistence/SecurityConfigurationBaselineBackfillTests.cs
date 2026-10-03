using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Identity;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves each baseline the Backfill-Security-Configuration-Baseline-Activity migration writes is the entry
/// <see cref="ActivityLogEntryFactory"/> builds for the same baseline event.
/// </summary>
/// <remarks>
/// The payloads are hand-built T-SQL, a connection's settings read out of its configuration JSON, so only a real SQL
/// Server shows they read back the way the serializer writes them. The expected settings are taken from the
/// creation event, which is how the connector itself describes them.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class SecurityConfigurationBaselineBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = InstallSecretProtector(fixture);

    /// <summary>
    /// Connections encrypt their credentials on save, through a protector the host installs at startup. Installed
    /// here only when nothing has, so a protector another test set is never swapped from under it.
    /// </summary>
    private static SqlServerDbContextFixture InstallSecretProtector(SqlServerDbContextFixture fixture)
    {
        try
        {
            _ = SecretProtectorAccessor.Current;
        }
        catch (InvalidOperationException)
        {
            SecretProtectorAccessor.Set(new AesGcmSecretProtector(RandomNumberGenerator.GetBytes(32)));
        }

        return fixture;
    }

    [Fact]
    public async Task Backfill_WritesThePersonalAccessTokenBaselineTheFactoryWould()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (token, created) = await Seed(ct, async context =>
        {
            var user = await SeedUser(context, ct);
            return PersonalAccessToken.Create("Build agent", "abcd1234", $"hash-{Guid.NewGuid():N}", user.Id,
                Now.Plus(Duration.FromDays(30)), "[\"Permissions.Projects.View\"]", EventActor.System, Now).Value;
        });
        var createdEvent = (PersonalAccessTokenCreatedEvent)created;

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(token, (recordCreatedOn, timestamp) => new PersonalAccessTokenBaselinedEvent(
            token.Id, createdEvent.UserId, createdEvent.Name, createdEvent.ExpiresAt, createdEvent.Scopes,
            recordCreatedOn, null, timestamp), ct);
    }

    [Fact]
    public async Task Backfill_SkipsARevokedToken()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (token, _) = await Seed(ct, async context =>
        {
            var user = await SeedUser(context, ct);
            var pat = PersonalAccessToken.Create("Old agent", "efgh5678", $"hash-{Guid.NewGuid():N}", user.Id,
                Now.Plus(Duration.FromDays(30)), null, EventActor.System, Now).Value;
            pat.Revoke(user.Id, EventActor.System, Now);
            return pat;
        });

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.AnyAsync(a => a.AggregateId == token.Id, ct)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Backfill_WritesTheOidcProviderBaselineTheFactoryWould(bool entra)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (provider, created) = await Seed(ct, async context => entra
            ? OidcProvider.Create($"entra-{suffix}", "Acme \"Entra\"", OidcProviderType.MicrosoftEntraId,
                "https://login.microsoftonline.com/common/v2.0", "client-1", "api://client-1", ["openid", "profile"],
                ["11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"], 120, true, EventActor.System, Now,
                RegistrationPolicy.Enabled(requireEmployeeRecord: true, defaultRoleId: await SeedRole(context, ct))).Value
            : OidcProvider.Create($"okta-{suffix}", "Acme Okta", OidcProviderType.GenericOidc,
                "https://acme.okta.example", "client-2", "client-2", ["openid"], null, 60, false, EventActor.System, Now).Value);
        var createdEvent = (OidcProviderCreatedEvent)created;

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(provider, (recordCreatedOn, timestamp) => new OidcProviderBaselinedEvent(
            provider.Id, createdEvent.Name, createdEvent.Label, createdEvent.ProviderType, createdEvent.Configuration,
            createdEvent.RegistrationPolicy, createdEvent.IsEnabled, recordCreatedOn, null, timestamp), ct);
    }

    public static TheoryData<string> Connectors() => new(nameof(Connector.AzureDevOps), nameof(Connector.AzureOpenAI), nameof(Connector.Entra), nameof(Connector.Workday));

    [Theory]
    [MemberData(nameof(Connectors))]
    public async Task Backfill_WritesTheConnectionBaselineTheFactoryWould(string connector)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (connection, created) = await Seed(ct, _ => Task.FromResult(CreateConnection(Enum.Parse<Connector>(connector))));
        var createdEvent = (ConnectionCreatedEvent)created;

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(connection, (recordCreatedOn, timestamp) => new ConnectionBaselinedEvent(
            connection.Id, createdEvent.Name, createdEvent.Description, createdEvent.Connector, createdEvent.IsActive,
            createdEvent.Settings, recordCreatedOn, null, timestamp), ct);
    }

    [Fact]
    public async Task Backfill_RunTwice_WritesOneBaseline()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (connection, _) = await Seed(ct, _ => Task.FromResult(CreateConnection(Connector.Entra)));
        await RunBackfill(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.CountAsync(a => a.AggregateId == connection.Id, ct)).Should().Be(1);
    }

    private static Connection CreateConnection(Connector connector) => connector switch
    {
        Connector.AzureDevOps => AzureDevOpsBoardsConnection.Create("Boards", "Work items", "system-1",
            new AzureDevOpsBoardsConnectionConfiguration("acme", "ado-pat"), true, null, EventActor.System, Now),
        Connector.AzureOpenAI => AzureOpenAIConnection.Create("AI", null,
            new AzureOpenAIConnectionConfiguration("api-key", "gpt-4o", "https://acme.openai.example", 0.1, 800, false), true, EventActor.System, Now),
        Connector.Entra => EntraConnection.Create("People", null,
            new EntraConnectionConfiguration("tenant-1", "client-1", "client-secret", "group-1", true, EmployeeMatchProperty.EmployeeNumber, false),
            true, EventActor.System, Now),
        Connector.Workday => WorkdayConnection.Create("Workers", null,
            new WorkdayConnectionConfiguration("https://wd.acme.example/ccx/service/acme_corp/Staffing/v46.1?wsdl", "isu-user", "isu-pass",
                WorkdayWorkerKey.Wid, includeInactive: true, orgExclusions:
                [
                    new WorkdayOrgExclusion("SUPERVISORY", "org-b", "Org B"),
                    new WorkdayOrgExclusion("COST_CENTER", "org-a", "Org A"),
                ]),
            false, EventActor.System, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(connector)),
    };

    /// <summary>
    /// Saves the record, then removes the entries its save wrote, leaving it as every such record was when the
    /// migration ran: on file with no history. Returns the creation event, which describes the record as created.
    /// </summary>
    private async Task<(TEntity Entity, DomainEvent Created)> Seed<TEntity>(CancellationToken ct, Func<WaydDbContext, Task<TEntity>> create)
        where TEntity : BaseEntity<Guid>
    {
        await using var context = _fixture.CreateContext();
        var entity = await create(context);
        var created = entity.DomainEvents.First();

        context.Add(entity);
        await context.SaveChangesAsync(ct);

        await context.ActivityLogs.Where(a => a.AggregateId == entity.Id).ExecuteDeleteAsync(ct);

        return (entity, created);
    }

    private static async Task<ApplicationUser> SeedUser(WaydDbContext context, CancellationToken ct)
    {
        var userName = $"owner-{Guid.NewGuid():N}@acme.example";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = userName,
            NormalizedEmail = userName.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true,
            LoginProvider = LoginProviders.Wayd,
        };
        context.Set<ApplicationUser>().Add(user);
        await context.SaveChangesAsync(ct);
        return user;
    }

    private static async Task<string> SeedRole(WaydDbContext context, CancellationToken ct)
    {
        var role = new ApplicationRole($"Backfill {Guid.NewGuid():N}") { NormalizedName = $"BACKFILL {Guid.NewGuid():N}" };
        context.Set<ApplicationRole>().Add(role);
        await context.SaveChangesAsync(ct);
        return role.Id;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = BackfillSecurityConfigurationBaselineActivity.UpSql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task AssertBaseline<TEntity>(TEntity entity, Func<Instant?, Instant, DomainEvent> baseline, CancellationToken ct)
        where TEntity : BaseEntity<Guid>
    {
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking()
            .Include(a => a.RelatedAggregates)
            .SingleAsync(a => a.AggregateId == entity.Id, ct);
        var recordCreatedOn = await verify.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => e.Id == entity.Id)
            .Select(e => EF.Property<Instant>(e, "SystemCreated"))
            .SingleAsync(ct);

        var expectedEvent = baseline(recordCreatedOn, entry.Timestamp);
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, entity, 0, null);

        entry.EventId.Should().Be(expectedEvent.EventId);
        entry.Should().BeEquivalentTo(expected, options => options
            .Including(e => e.EventType)
            .Including(e => e.Category)
            .Including(e => e.EventVersion)
            .Including(e => e.DomainArea)
            .Including(e => e.AggregateType)
            .Including(e => e.AggregateId)
            .Including(e => e.ActorKind)
            .Including(e => e.UserId)
            .Including(e => e.EmployeeId)
            .Including(e => e.Ordinal)
            .Including(e => e.CorrelationId)
            .Including(e => e.Summary));
        entry.RelatedAggregates.Select(r => (r.AggregateType, r.AggregateId))
            .Should().BeEquivalentTo(expected.RelatedAggregates.Select(r => (r.AggregateType, r.AggregateId)));
        JsonNode.DeepEquals(JsonNode.Parse(entry.Payload), JsonNode.Parse(expected.Payload))
            .Should().BeTrue($"the backfilled payload {entry.Payload} should match the serialized event {expected.Payload}");
    }
}
