using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.Common.Application.Identity;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Models;
using Wayd.Infrastructure.Auditing;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves an audit trail row records each property in a form no weaker than its own column: a connection's
/// configuration as the encrypted JSON the column holds, and an excluded property not at all.
/// </summary>
/// <remarks>
/// A real provider is what shows it: the stored form comes from the value converter EF applies on save, and the
/// trail is written inside the same save.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class AuditTrailValueTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 9, 0, 0);

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
    public async Task SaveChanges_CreatingAConnection_RecordsItsSecretEncrypted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var apiKey = $"plain-key-{Guid.NewGuid():N}";
        await using var context = _fixture.CreateContext();
        var connection = AzureOpenAIConnection.Create("AI", null,
            new AzureOpenAIConnectionConfiguration(apiKey, "gpt-4o", "https://acme.openai.example"), true, EventActor.System, Now);
        context.Add(connection);

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        var trail = await SingleTrail(nameof(AzureOpenAIConnection), connection.Id, ct);
        trail.NewValues.Should().NotContain(apiKey);

        var configuration = JsonNode.Parse(trail.NewValues!)!["configuration"]!;
        configuration["DeploymentName"]!.GetValue<string>().Should().Be("gpt-4o", "settings that are not secret stay readable");
        var storedKey = configuration["ApiKey"]!.GetValue<string>();
        SecretProtectorAccessor.Current.IsProtected(storedKey).Should().BeTrue();
        SecretProtectorAccessor.Current.Unprotect(storedKey).Should().Be(apiKey);
    }

    [Fact]
    public async Task SaveChanges_ChangingAConnectionsSecret_RecordsNeitherValueInPlainText()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var oldKey = $"old-key-{Guid.NewGuid():N}";
        var newKey = $"new-key-{Guid.NewGuid():N}";
        Guid id;
        await using (var seed = _fixture.CreateContext())
        {
            var created = AzureOpenAIConnection.Create("AI", null,
                new AzureOpenAIConnectionConfiguration(oldKey, "gpt-4o", "https://acme.openai.example"), true, EventActor.System, Now);
            seed.Add(created);
            await seed.SaveChangesAsync(ct);
            id = created.Id;
        }

        await using var context = _fixture.CreateContext();
        var connection = await context.Set<AzureOpenAIConnection>().SingleAsync(c => c.Id == id, ct);
        connection.Update("AI", null, newKey, "gpt-4o", true, EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        var update = (await Trails(nameof(AzureOpenAIConnection), id, ct)).Single(t => t.Type == nameof(TrailType.Update));
        update.AffectedColumns.Should().Contain("Configuration");
        update.OldValues.Should().NotContain(oldKey);
        update.NewValues.Should().NotContain(newKey);

        var recordedNewKey = JsonNode.Parse(update.NewValues!)!["configuration"]!["ApiKey"]!.GetValue<string>();
        SecretProtectorAccessor.Current.Unprotect(recordedNewKey).Should().Be(newKey);
    }

    [Fact]
    public async Task SaveChanges_RecordingTimeAway_LeavesOutTheReason()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var capacity = EmployeeCapacity.Create((await SeedEmployee(context, ct)).Id);
        var timeAway = capacity.RecordTimeAway(new LocalDate(2026, 7, 6), new LocalDate(2026, 7, 10), 100,
            TimeAwayReason.PaidTimeOff, EventActor.System, Now).Value;
        context.EmployeeCapacities.Add(capacity);

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        var trail = await SingleTrail(nameof(EmployeeTimeAway), timeAway.Id, ct);
        var values = JsonNode.Parse(trail.NewValues!)!.AsObject();
        values.Should().ContainKey("percentAway", "only the reason is excluded");
        values.Should().NotContainKey("reason");
    }

    [Fact]
    public async Task SaveChanges_ChangingOnlyTheReason_RecordsThatItChangedWithoutEitherValue()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Guid employeeId;
        Guid timeAwayId;
        await using (var seed = _fixture.CreateContext())
        {
            var created = EmployeeCapacity.Create((await SeedEmployee(seed, ct)).Id);
            timeAwayId = created.RecordTimeAway(new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 7), 100,
                TimeAwayReason.PaidTimeOff, EventActor.System, Now).Value.Id;
            seed.EmployeeCapacities.Add(created);
            await seed.SaveChangesAsync(ct);
            employeeId = created.Id;
        }

        await using var context = _fixture.CreateContext();
        var capacity = await context.EmployeeCapacities.Include(c => c.TimeAway).SingleAsync(c => c.Id == employeeId, ct);
        capacity.ChangeTimeAway(timeAwayId, new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 7), 100,
            TimeAwayReason.BusinessTravel, EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        var update = (await Trails(nameof(EmployeeTimeAway), timeAwayId, ct)).Single(t => t.Type == nameof(TrailType.Update));
        update.AffectedColumns.Should().Contain("Reason");
        update.OldValues.Should().BeNull();
        update.NewValues.Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_CreatingAPersonalAccessToken_LeavesOutItsHash()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var hash = $"hash-{Guid.NewGuid():N}";
        await using var context = _fixture.CreateContext();
        var user = await SeedUser(context, ct);
        var token = PersonalAccessToken.Create("Build agent", "abcd1234", hash, user.Id,
            Now.Plus(Duration.FromDays(30)), TokenScope.Read, EventActor.System, Now).Value;
        context.Add(token);

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        var trail = await SingleTrail(nameof(PersonalAccessToken), token.Id, ct);
        trail.NewValues.Should().Contain("Build agent").And.NotContain(hash);
    }

    private async Task<Trail> SingleTrail(string tableName, Guid id, CancellationToken ct) =>
        (await Trails(tableName, id, ct)).Should().ContainSingle().Subject;

    private async Task<List<Trail>> Trails(string tableName, Guid id, CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        return await verify.AuditTrails.AsNoTracking()
            .Where(t => t.TableName == tableName && t.PrimaryKey!.Contains(id.ToString()))
            .OrderBy(t => t.Id)
            .ToListAsync(ct);
    }

    /// <summary>A real employee, because the capacity record has a foreign key to one.</summary>
    private static async Task<Employee> SeedEmployee(WaydDbContext context, CancellationToken ct)
    {
        var employee = Employee.Create(
            new PersonName("Audit", null, "Probe"),
            $"{Guid.CreateVersion7():N}"[..12],
            Now,
            new EmailAddress($"{Guid.CreateVersion7():N}"[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Now);

        context.Employees.Add(employee);
        await context.SaveChangesAsync(ct);
        return employee;
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
}
