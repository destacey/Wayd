using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the intake commands against SQL Server: their visibility checks and lookups translate, and what they
/// change is saved and recorded.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DemandManagementCommandTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly IDateTimeProvider _clock = Clock();

    [Fact]
    public async Task ADeskAdministratorAndARequester_CreateADeskThenSubmitEditAndShareARequest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Employee requester, reviewer, colleague;
        IntakeChannel channel;
        await using (var seeding = _fixture.CreateContext())
        {
            requester = SeedEmployee(seeding);
            reviewer = SeedEmployee(seeding);
            colleague = SeedEmployee(seeding);
            channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
            seeding.IntakeChannels.Add(channel);
            await seeding.SaveChangesAsync(ct);
        }

        // Act
        await using var context = _fixture.CreateContext();
        var desk = await new CreateIntakeDeskCommandHandler(context, Principal(reviewer.Id), User(), _clock).Handle(
            new CreateIntakeDeskCommand($"Desk-{Guid.NewGuid():N}"[..30], "Internal tools", IntakeDeskVisibility.SummaryOnly,
                true, true, true, [reviewer.Id]), ct);
        var created = await new CreateIntakeRequestCommandHandler(context, Principal(requester.Id), User(), _clock).Handle(
            new CreateIntakeRequestCommand("Export invoices", "Finance needs a CSV export", null, desk.Value.Id, channel.Id,
                null, null), ct);
        var updated = await new UpdateIntakeRequestCommandHandler(context, Principal(requester.Id), User(), _clock).Handle(
            new UpdateIntakeRequestCommand(created.Value.Id, "Export invoices to CSV", "Finance needs a CSV export", null,
                channel.Id, new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.RequesterPreference, "Quarter end", null, null)), ct);
        var shared = await new AddIntakeRequestParticipantCommandHandler(context, Principal(requester.Id), User(), _clock).Handle(
            new AddIntakeRequestParticipantCommand(created.Value.Id, colleague.Id), ct);
        var assigned = await new AssignIntakeRequestReviewerCommandHandler(context, Principal(reviewer.Id, IntakeRequestAuthorization.ReviewPermission), User(), _clock).Handle(
            new AssignIntakeRequestReviewerCommand(created.Value.Id, reviewer.Id), ct);

        // Assert
        new[] { desk.IsSuccess, created.IsSuccess, updated.IsSuccess, shared.IsSuccess, assigned.IsSuccess }.Should().AllBeEquivalentTo(true);

        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().Include(r => r.Participants).SingleAsync(r => r.Id == created.Value.Id, ct);
        saved.Title.Should().Be("Export invoices to CSV");
        saved.NeedBy!.Basis.Should().Be(NeedByBasis.RequesterPreference);
        saved.Participants.Select(p => p.EmployeeId).Should().Equal(colleague.Id);
        saved.AssignedReviewerId.Should().Be(reviewer.Id);

        var events = await reader.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == created.Value.Id)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .Select(a => a.EventType)
            .ToListAsync(ct);
        events.Should().Equal(
            nameof(IntakeRequestCreatedEvent),
            nameof(IntakeRequestRetitledEvent),
            nameof(IntakeRequestNeedByChangedEvent),
            nameof(IntakeRequestParticipantAddedEvent),
            nameof(IntakeRequestReviewerAssignedEvent));
    }

    [Fact]
    public async Task DeleteIntakeRequest_RemovesTheRequestAndItsParticipants()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Guid requestId;
        Employee requester;
        await using (var seeding = _fixture.CreateContext())
        {
            requester = SeedEmployee(seeding);
            var participant = SeedEmployee(seeding);
            var desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true, [], EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
            seeding.IntakeDesks.Add(desk);
            seeding.IntakeChannels.Add(channel);
            await seeding.SaveChangesAsync(ct);
            var request = IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk, channel, null, null, EventActor.System, Now).Value;
            request.AddParticipant(participant.Id, null, EventActor.System, Now);
            seeding.IntakeRequests.Add(request);
            await seeding.SaveChangesAsync(ct);
            requestId = request.Id;
        }

        // Act
        await using var context = _fixture.CreateContext();
        var result = await new DeleteIntakeRequestCommandHandler(context, Principal(requester.Id), User(), _clock)
            .Handle(new DeleteIntakeRequestCommand(requestId), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeRequests.AnyAsync(r => r.Id == requestId, ct)).Should().BeFalse();
        (await reader.ActivityLogs.AnyAsync(a => a.AggregateId == requestId && a.EventType == nameof(IntakeRequestDeletedEvent), ct)).Should().BeTrue();
    }

    private static ICurrentPrincipal Principal(Guid employeeId, params string[] permissions)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) => permissions.Contains(permission));
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) => permissions.Contains(permission));
        return principal.Object;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        return clock.Object;
    }

    private static ICurrentUser User()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.GetUserId()).Returns("intake-probe");
        return user.Object;
    }

    private static Employee SeedEmployee(WaydDbContext context)
    {
        var employee = Employee.Create(
            new PersonName("Intake", null, "Probe"),
            Guid.NewGuid().ToString("N")[..12],
            Now,
            new EmailAddress(Guid.NewGuid().ToString("N")[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Now);

        context.Employees.Add(employee);
        return employee;
    }
}
