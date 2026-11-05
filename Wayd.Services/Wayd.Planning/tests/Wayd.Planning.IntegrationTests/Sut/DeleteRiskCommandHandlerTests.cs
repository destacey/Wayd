using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Risks;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;
using Wayd.Planning.Application.Risks.Commands;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Proves a risk's deletion soft deletes the row and is recorded against the risk by the same save.
/// </summary>
/// <remarks>
/// The soft delete turns the removal into an update, so only a real save shows the event is still drained from
/// an entity that save is removing, and that the row survives with its history.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeleteRiskCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_SoftDeletesTheRiskAndRecordsTheDeletion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        Guid riskId;
        int riskKey;
        await using (var seed = _fixture.CreateContext())
        {
            var team = await PlanningSeed.Team(seed, ct);
            var reporter = Employee.Create(new PersonName("Ada", null, "Lovelace"), "E" + Guid.NewGuid().ToString("N")[..11], now,
                new EmailAddress($"ada.{Guid.NewGuid():N}@acme.example"), null, null, null, null, true, null, EventActor.System, now);
            seed.Employees.Add(reporter);
            await seed.SaveChangesAsync(ct);

            var risk = Risk.Create("Vendor slip", null, team.Id, now, reporter.Id, RiskCategory.Owned, RiskGrade.High, RiskGrade.Low,
                null, null, null, EventActor.System, now);
            seed.Risks.Add(risk);
            await seed.SaveChangesAsync(ct);

            riskId = risk.Id;
            riskKey = risk.Key;
        }

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext())
        {
            var handler = new DeleteRiskCommandHandler(context, SqlServerDbContextFixture.CurrentUser(),
                SqlServerDbContextFixture.DateTimeProvider(now.Plus(Duration.FromHours(1))), NullLogger<DeleteRiskCommandHandler>.Instance);
            result = await handler.Handle(new DeleteRiskCommand(riskId), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.Risks.AnyAsync(r => r.Id == riskId, ct)).Should().BeFalse();
        (await verify.Risks.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.Id == riskId)
            .Select(r => r.IsDeleted)
            .SingleAsync(ct)).Should().BeTrue();

        var entries = await verify.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == riskId)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .ToListAsync(ct);

        entries.Select(e => e.EventType).Should().Equal(
            nameof(RiskCreatedEvent),
            nameof(RiskDeletedEvent));
        entries.Should().OnlyContain(e => e.AggregateType == "Risk"
            && JsonDocument.Parse(e.Payload).RootElement.GetProperty("key").GetInt32() == riskKey);
        entries[^1].Category.Should().Be(ActivityCategory.Removed);
        JsonDocument.Parse(entries[^1].Payload).RootElement.GetProperty("summary").GetString().Should().Be("Vendor slip");
    }
}
