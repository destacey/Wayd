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
    public async Task SaveChanges_PersistsADeskWithItsMembersInTheirRoles_AndRecordsItsCreationWithTheAssignedKey()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var deskAdmin = await SeedEmployee(context);
        var agent = await SeedEmployee(context);
        var desk = IntakeDesk.Create(UniqueName("Desk"), "Internal tools", IntakeDeskVisibility.SummaryOnly,
            isRequesterSelectable: true, isOpen: true, requesterMayAddParticipants: false,
            [new IntakeDeskMemberDetails(deskAdmin.Id, IntakeDeskRole.Admin), new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)],
            EventActor.System, Now);

        // Act
        context.IntakeDesks.Add(desk);
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeDesks.AsNoTracking().Include(d => d.Members).SingleAsync(d => d.Id == desk.Id, ct);
        saved.Key.Should().BePositive();
        saved.Visibility.Should().Be(IntakeDeskVisibility.SummaryOnly);
        saved.RequesterMayAddParticipants.Should().BeFalse();
        saved.Members.Should().HaveCount(2);
        saved.RoleOf(deskAdmin.Id).Should().Be(IntakeDeskRole.Admin);
        saved.RoleOf(agent.Id).Should().Be(IntakeDeskRole.Agent);

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
            requester.Id, requester.Id, desk, channel, needBy, origin, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
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
        eventTypes.Should().Equal(nameof(IntakeRequestCreatedEventV2), nameof(IntakeRequestParticipantAddedEvent));
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
            desk, channel, null, null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;

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
            channel, null, IntakeRequestOrigin.Create("ServicePortal", externalId, null).Value, IntakeStatuses.Submitted(), EventActor.System, Now).Value;

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
            null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
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

    [Fact]
    public async Task SaveChanges_ReplacingTheNeedByWithAnother_PersistsTheNewValue()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var (desk, channel) = await SeedDeskAndChannel(context);
        var requester = await SeedEmployee(context);
        var from = IntakeRequestNeedBy.Create(new LocalDate(2026, 12, 31), NeedByBasis.Regulatory, "Rule 1", null, null).Value;
        var to = IntakeRequestNeedBy.Create(new LocalDate(2027, 3, 31), NeedByBasis.Commitment, "Steering committee",
            requester.Id, new LocalDate(2026, 9, 3)).Value;
        var request = IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk, channel, from,
            null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
        context.IntakeRequests.Add(request);
        await context.SaveChangesAsync(ct);

        // Act
        request.ChangeNeedBy(to, IntakeRequestEditor.System, Now.Plus(Duration.FromMinutes(1))).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id, ct)).NeedBy.Should().Be(to);
    }

    [Fact]
    public async Task SaveChanges_PersistsARequestWithNoChannel_AndRecordsItsCreationWithoutOne()
    {
        // Arrange — submitted to a desk that offers no channel
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var (desk, _) = await SeedDeskAndChannel(context);
        var requester = await SeedEmployee(context);
        var request = IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk, null, null,
            null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;

        // Act
        context.IntakeRequests.Add(request);
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id, ct);
        saved.ChannelId.Should().BeNull();
        var entry = await reader.ActivityLogs.AsNoTracking()
            .SingleAsync(a => a.AggregateId == request.Id && a.EventType == nameof(IntakeRequestCreatedEventV2), ct);
        entry.Payload.Should().Contain("\"channelId\":null");
    }

    [Fact]
    public async Task SaveChanges_ChoosingADesksChannels_PersistsThemAndRecordsTheChange()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Guid deskId;
        Guid channelId;
        await using (var context = _fixture.CreateContext())
        {
            var (desk, channel) = await SeedDeskAndChannel(context);
            deskId = desk.Id;
            channelId = channel.Id;
        }

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var desk = await context.IntakeDesks.Include(d => d.Channels).SingleAsync(d => d.Id == deskId, ct);
            desk.ChangeChannels(false, [channelId], EventActor.System, Now).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
        }

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeDesks.AsNoTracking().Include(d => d.Channels).SingleAsync(d => d.Id == deskId, ct);
        saved.OffersAllChannels.Should().BeFalse();
        saved.Channels.Select(c => c.ChannelId).Should().Equal(channelId);
        (await reader.ActivityLogs.AsNoTracking()
            .AnyAsync(a => a.AggregateId == deskId && a.EventType == nameof(IntakeDeskChannelsChangedEvent), ct))
            .Should().BeTrue();
    }

    [Fact]
    public async Task SaveChanges_ChoosingADesksTagAxes_PersistsThemAndRecordsTheChange()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Guid deskId;
        Guid categoryId;
        await using (var context = _fixture.CreateContext())
        {
            var (desk, _) = await SeedDeskAndChannel(context);
            var category = IntakeRequestTagCategory.Create(UniqueName("Axis"), null, allowsMany: true, 1, EventActor.System, Now);
            context.IntakeRequestTagCategories.Add(category);
            await context.SaveChangesAsync(ct);
            deskId = desk.Id;
            categoryId = category.Id;
        }

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var desk = await context.IntakeDesks.Include(d => d.TagCategories).SingleAsync(d => d.Id == deskId, ct);
            desk.ChangeTagCategories(false, [categoryId], EventActor.System, Now).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
        }

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeDesks.AsNoTracking().Include(d => d.TagCategories).SingleAsync(d => d.Id == deskId, ct);
        saved.OffersAllTagCategories.Should().BeFalse();
        saved.TagCategories.Select(c => c.CategoryId).Should().Equal(categoryId);
        (await reader.ActivityLogs.AsNoTracking()
            .AnyAsync(a => a.AggregateId == deskId && a.EventType == nameof(IntakeDeskTagCategoriesChangedEvent), ct))
            .Should().BeTrue();
    }

    [Fact]
    public async Task SaveChanges_DeletingATagAxisADeskChose_IsRefusedUnlessTheDeskLetsItGoInTheSameSave()
    {
        // Arrange — the key restricts, so only the desk itself can drop the axis, recording that it did
        var ct = TestContext.Current.CancellationToken;
        Guid deskId;
        Guid categoryId;
        await using (var context = _fixture.CreateContext())
        {
            var (desk, _) = await SeedDeskAndChannel(context);
            var category = IntakeRequestTagCategory.Create(UniqueName("Axis"), null, allowsMany: true, 1, EventActor.System, Now);
            context.IntakeRequestTagCategories.Add(category);
            await context.SaveChangesAsync(ct);
            desk.ChangeTagCategories(false, [category.Id], EventActor.System, Now).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
            deskId = desk.Id;
            categoryId = category.Id;
        }

        // Act
        Func<Task> deleteAlone = async () =>
        {
            await using var context = _fixture.CreateContext();
            var category = await context.IntakeRequestTagCategories.Include(c => c.Tags).SingleAsync(c => c.Id == categoryId, ct);
            context.IntakeRequestTagCategories.Remove(category);
            await context.SaveChangesAsync(ct);
        };
        await deleteAlone.Should().ThrowAsync<DbUpdateException>();

        await using (var context = _fixture.CreateContext())
        {
            var desk = await context.IntakeDesks.Include(d => d.TagCategories).SingleAsync(d => d.Id == deskId, ct);
            var category = await context.IntakeRequestTagCategories.Include(c => c.Tags).SingleAsync(c => c.Id == categoryId, ct);
            desk.ChangeTagCategories(false, [], EventActor.System, Now).IsSuccess.Should().BeTrue();
            context.IntakeRequestTagCategories.Remove(category);
            await context.SaveChangesAsync(ct);
        }

        // Assert
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeRequestTagCategories.AnyAsync(c => c.Id == categoryId, ct)).Should().BeFalse();
        (await reader.IntakeDesks.AsNoTracking().Include(d => d.TagCategories).SingleAsync(d => d.Id == deskId, ct))
            .TagCategories.Should().BeEmpty();
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];

    private static async Task<(IntakeDesk Desk, IntakeChannel Channel)> SeedDeskAndChannel(WaydDbContext context)
    {
        var desk = IntakeDesk.Create(UniqueName("Desk"), "Anything", IntakeDeskVisibility.Open, true, true, true, [],
            EventActor.System, Now);
        var channel = IntakeChannel.Create(UniqueName("Channel"), null, 1, EventActor.System, Now);
        context.IntakeDesks.Add(desk);
        context.IntakeChannels.Add(channel);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (desk, channel);
    }

    /// <summary>A real employee, because requests and desk members have foreign keys to one.</summary>
    private static async Task<Employee> SeedEmployee(WaydDbContext context)
    {
        var employee = Employee.Create(
            new PersonName("Intake", null, "Probe"),
            $"{Guid.NewGuid():N}"[..12],
            Now,
            new EmailAddress($"{Guid.NewGuid():N}"[..12] + "@acme.example"),
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
