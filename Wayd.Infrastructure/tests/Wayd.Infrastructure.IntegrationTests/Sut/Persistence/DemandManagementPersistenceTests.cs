using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Models;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves desks and requests survive a real save: the owned values and child rows round-trip, the keys the
/// database assigns reach the events, and the constraints the aggregates cannot see hold.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DemandManagementPersistenceTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task SaveChanges_PersistsADeskWithItsReviewers_AndRecordsItsCreationWithTheAssignedKey()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var reviewer = await SeedEmployee(context);
        var desk = IntakeDesk.Create(UniqueName("Desk"), "Internal tools", IntakeDeskVisibility.SummaryOnly,
            isRequesterSelectable: true, isOpen: true, requesterMayAddParticipants: false, [reviewer.Id],
            EventActor.System, Now);

        // Act
        context.IntakeDesks.Add(desk);
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeDesks.AsNoTracking().Include(d => d.Reviewers).SingleAsync(d => d.Id == desk.Id, ct);
        saved.Key.Should().BePositive();
        saved.Visibility.Should().Be(IntakeDeskVisibility.SummaryOnly);
        saved.RequesterMayAddParticipants.Should().BeFalse();
        saved.Reviewers.Select(r => r.EmployeeId).Should().Equal(reviewer.Id);
        saved.IsReviewer(reviewer.Id).Should().BeTrue();

        var entry = await reader.ActivityLogs.AsNoTracking().SingleAsync(a => a.AggregateId == desk.Id, ct);
        entry.EventType.Should().Be(nameof(IntakeDeskCreatedEvent));
        entry.DomainArea.Should().Be("DemandManagement");
        entry.Payload.Should().Contain($"\"key\":{saved.Key}");
    }

    [Fact]
    public async Task SaveChanges_PersistsARequestWithItsNeedByOriginAndParticipants()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var (desk, channel) = await SeedDeskAndChannel(context);
        var requester = await SeedEmployee(context);
        var committer = await SeedEmployee(context);
        var participant = await SeedEmployee(context);
        var needBy = IntakeRequestNeedBy.Create(new LocalDate(2026, 12, 31), NeedByBasis.Commitment, "Steering committee",
            committer.Id, new LocalDate(2026, 9, 3)).Value;
        var origin = IntakeRequestOrigin.Create("ServicePortal", UniqueName("REQ"), "https://portal.acme.example/req").Value;
        var request = IntakeRequest.Create("Export invoices", "Finance needs a CSV export", "A day a month back",
            requester.Id, requester.Id, desk, channel, needBy, origin, EventActor.System, Now).Value;
        request.AddParticipant(participant.Id, requester.Id, EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        context.IntakeRequests.Add(request);
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().Include(r => r.Participants).SingleAsync(r => r.Id == request.Id, ct);
        saved.Key.Should().BePositive();
        saved.ExpectedBenefits.Should().Be("A day a month back");
        saved.NeedBy.Should().Be(needBy);
        saved.Origin.Should().Be(origin);
        saved.Participants.Should().ContainSingle(p => p.EmployeeId == participant.Id && p.AddedById == requester.Id && p.AddedOn == Now);

        var eventTypes = await reader.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == request.Id)
            .OrderBy(a => a.Ordinal)
            .Select(a => a.EventType)
            .ToListAsync(ct);
        eventTypes.Should().Equal(nameof(IntakeRequestCreatedEvent), nameof(IntakeRequestParticipantAddedEvent));
    }

    [Fact]
    public async Task SaveChanges_LoadsARequestWithoutNeedByOrOriginWithBothNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var (desk, channel) = await SeedDeskAndChannel(context);
        var requester = await SeedEmployee(context);
        var request = IntakeRequest.Create("Laptop refresh", "Replace the 2021 laptops", null, requester.Id, requester.Id,
            desk, channel, null, null, EventActor.System, Now).Value;

        // Act
        context.IntakeRequests.Add(request);
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id, ct);
        saved.NeedBy.Should().BeNull();
        saved.Origin.Should().BeNull();
        saved.ExpectedBenefits.Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_RefusesASecondRequestFromTheSameOrigin()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var (desk, channel) = await SeedDeskAndChannel(context);
        var requester = await SeedEmployee(context);
        var externalId = UniqueName("REQ");
        IntakeRequest FromOrigin() => IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk,
            channel, null, IntakeRequestOrigin.Create("ServicePortal", externalId, null).Value, EventActor.System, Now).Value;

        context.IntakeRequests.Add(FromOrigin());
        await context.SaveChangesAsync(ct);

        // Act
        await using var second = _fixture.CreateContext();
        second.IntakeRequests.Add(FromOrigin());
        var act = () => second.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChanges_ChangingTheNeedByAfterTheFirstSave_RecordsBothEnds()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var (desk, channel) = await SeedDeskAndChannel(context);
        var requester = await SeedEmployee(context);
        var from = IntakeRequestNeedBy.Create(new LocalDate(2026, 12, 31), NeedByBasis.Regulatory, "Rule 1", null, null).Value;
        var request = IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk, channel, from,
            null, EventActor.System, Now).Value;
        context.IntakeRequests.Add(request);
        await context.SaveChangesAsync(ct);

        // Act
        request.ChangeNeedBy(null, IntakeRequestEditor.System, Now.Plus(Duration.FromMinutes(1))).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id, ct)).NeedBy.Should().BeNull();
        var entry = await reader.ActivityLogs.AsNoTracking()
            .SingleAsync(a => a.AggregateId == request.Id && a.EventType == nameof(IntakeRequestNeedByChangedEvent), ct);
        entry.Category.Should().Be(ActivityCategory.ScheduleChanged);
        entry.Payload.Should().Contain("\"reference\":\"Rule 1\"").And.Contain("\"to\":null");
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.CreateVersion7():N}"[..30];

    private static async Task<(IntakeDesk Desk, IntakeChannel Channel)> SeedDeskAndChannel(WaydDbContext context)
    {
        var desk = IntakeDesk.Create(UniqueName("Desk"), "Anything", IntakeDeskVisibility.Open, true, true, true, [],
            EventActor.System, Now);
        var channel = IntakeChannel.Create(UniqueName("Channel"), null, 1);
        context.IntakeDesks.Add(desk);
        context.IntakeChannels.Add(channel);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (desk, channel);
    }

    /// <summary>A real employee, because requests and reviewers have foreign keys to one.</summary>
    private static async Task<Employee> SeedEmployee(WaydDbContext context)
    {
        var employee = Employee.Create(
            new PersonName("Intake", null, "Probe"),
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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee;
    }
}
