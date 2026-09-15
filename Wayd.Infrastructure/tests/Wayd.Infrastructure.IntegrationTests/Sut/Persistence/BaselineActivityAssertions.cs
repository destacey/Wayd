using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Identity;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Checks shared by the migrations that write baselines by hand in T-SQL.
/// </summary>
internal static class BaselineActivityAssertions
{
    /// <summary>The account <see cref="Infrastructure.SqlServerDbContextFixture"/> stamps into every SystemCreatedBy.</summary>
    public const string FixtureUserId = "integration-test-user";

    private static readonly string[] ComputedProperties = ["days", "effectiveStart", "effectiveEnd"];

    /// <summary>
    /// Asserts that a row written by a migration reads back as <paramref name="expected"/>, and matches the entry
    /// <see cref="ActivityLogEntryFactory"/> builds for it: payload, computed properties, envelope and derived id.
    /// </summary>
    public static void AssertBaseline<TEvent>(ActivityLogEntry row, TEvent expected)
        where TEvent : DomainEvent, IBaselineEvent
    {
        var because = $"the {typeof(TEvent).Name} for {expected.AggregateId}";

        var actual = JsonSerializer.Deserialize<TEvent>(row.Payload, ActivityLogEntryFactory.ActivityJsonOptions);
        actual.Should().NotBeNull(because);
        actual.Should().BeEquivalentTo(expected, because);

        actual!.EventId.Should().Be(row.Id, because);
        row.Id.Should().Be(BaselineEventId.For(expected.AggregateType, expected.AggregateId), because);

        var factoryEntry = ActivityLogEntryFactory.CreateActivityLogEntry(expected, expected, 0, null);
        new
        {
            row.Id, row.EventType, row.Category, row.EventVersion, row.DomainArea, row.AggregateType, row.AggregateId,
            row.ActorKind, row.UserId, row.EmployeeId, row.Summary, row.Timestamp, row.Ordinal, row.CorrelationId,
        }.Should().BeEquivalentTo(new
        {
            factoryEntry.Id, factoryEntry.EventType, factoryEntry.Category, factoryEntry.EventVersion, factoryEntry.DomainArea,
            factoryEntry.AggregateType, factoryEntry.AggregateId, factoryEntry.ActorKind, factoryEntry.UserId,
            factoryEntry.EmployeeId, factoryEntry.Summary, factoryEntry.Timestamp, factoryEntry.Ordinal, factoryEntry.CorrelationId,
        }, because);

        // Property names and computed values are checked on the raw JSON: deserializing ignores both, so a
        // payload missing a computed property would still compare equal as an object.
        using var actualJson = JsonDocument.Parse(row.Payload);
        using var expectedJson = JsonDocument.Parse(factoryEntry.Payload);

        PropertyPaths(actualJson.RootElement, "$").Distinct().Should()
            .BeEquivalentTo(PropertyPaths(expectedJson.RootElement, "$").Distinct(), because);

        ComputedValues(actualJson.RootElement, "$").Should()
            .BeEquivalentTo(ComputedValues(expectedJson.RootElement, "$"), because);
    }

    public static async Task<ActivityLogEntry> SingleRow(WaydDbContext context, Guid aggregateId, CancellationToken ct)
    {
        var rows = await context.ActivityLogs.AsNoTracking().Where(a => a.AggregateId == aggregateId).ToListAsync(ct);
        return rows.Should().ContainSingle().Subject;
    }

    public static async Task<Instant> SystemCreated<TEntity>(IQueryable<TEntity> set, Guid id, CancellationToken ct)
        where TEntity : class =>
        await set.IgnoreQueryFilters()
            .Where(e => EF.Property<Guid>(e, "Id") == id)
            .Select(e => EF.Property<Instant>(e, "SystemCreated"))
            .SingleAsync(ct);

    /// <summary>
    /// Makes the account the fixture records as every row's creator resolve to <paramref name="employeeId"/>.
    /// </summary>
    public static async Task LinkFixtureUserTo(WaydDbContext context, Guid employeeId, CancellationToken ct)
    {
        var user = await context.Set<ApplicationUser>().SingleOrDefaultAsync(u => u.Id == FixtureUserId, ct);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = FixtureUserId,
                UserName = "atlas.creator@acme.example",
                NormalizedUserName = "ATLAS.CREATOR@ACME.EXAMPLE",
                Email = "atlas.creator@acme.example",
                NormalizedEmail = "ATLAS.CREATOR@ACME.EXAMPLE",
                SecurityStamp = Guid.NewGuid().ToString(),
                IsActive = true,
                LoginProvider = LoginProviders.Wayd,
            };
            context.Set<ApplicationUser>().Add(user);
        }

        user.EmployeeId = employeeId;
        await context.SaveChangesAsync(ct);
    }

    private static IEnumerable<string> PropertyPaths(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var child = $"{path}.{property.Name}";
                yield return child;

                foreach (var descendant in PropertyPaths(property.Value, child))
                {
                    yield return descendant;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var descendant in PropertyPaths(item, $"{path}[]"))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static Dictionary<string, string> ComputedValues(JsonElement element, string path)
    {
        var values = new Dictionary<string, string>();
        Collect(element, path);
        return values;

        void Collect(JsonElement current, string currentPath)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in current.EnumerateObject())
                {
                    var child = $"{currentPath}.{property.Name}";
                    if (ComputedProperties.Contains(property.Name))
                    {
                        values[child] = property.Value.GetRawText();
                    }

                    Collect(property.Value, child);
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in current.EnumerateArray())
                {
                    Collect(item, $"{currentPath}[{index++}]");
                }
            }
        }
    }
}
