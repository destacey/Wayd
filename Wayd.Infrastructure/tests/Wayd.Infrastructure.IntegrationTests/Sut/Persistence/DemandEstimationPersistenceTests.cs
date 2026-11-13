using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.DemandManagement;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Common.Domain.Models.Planning.EstimationScales;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.DemandEstimationScales.Queries;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Models.Estimation;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves Demand Management's estimation scale copies and request estimates survive a real save, that the queries
/// reading them translate, and that the foreign keys the replication handler steers around hold.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DemandEstimationPersistenceTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task SaveChanges_PersistsAScaleCopyWithEveryVersionAndItsValuesInOrder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var v1 = new EstimationScaleVersionValues(Guid.NewGuid(), 1, [new("XL", true), new("S", true), new("?", false), new("M", true)]);
        var v2 = new EstimationScaleVersionValues(Guid.NewGuid(), 2, [new("S", true), new("M", true)]);
        var scale = new DemandEstimationScale(new EstimationScaleSnapshot(Guid.NewGuid(), 9, UniqueName("Sizes"), true, [v1, v2]), Now);

        // Act
        context.DemandEstimationScales.Add(scale);
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.DemandEstimationScales.AsNoTracking().Include(s => s.Versions).SingleAsync(s => s.Id == scale.Id, ct);
        saved.Key.Should().Be(9);
        saved.IsActive.Should().BeTrue();
        saved.Watermarks.Should().Be(DemandEstimationScaleWatermarks.At(Now));
        saved.Versions.Select(v => v.Number).Should().BeEquivalentTo([1, 2]);
        saved.Versions.Single(v => v.Number == 1).Values.Select(v => (v.Value, v.IsEstimate))
            .Should().Equal(("XL", true), ("S", true), ("?", false), ("M", true));
        saved.CurrentVersion.Id.Should().Be(v2.VersionId);
    }

    [Fact]
    public async Task SaveChanges_PersistsAPinnedDeskAndAnEstimate_AndRecordsBothEndsOfTheChange()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, scale, requester) = await SeedEstimatingDesk(ct);
        await using var context = _fixture.CreateContext();
        var request = await SeedRequest(context, desk, requester, ct);
        var tracked = await context.IntakeRequests.SingleAsync(r => r.Id == request.Id, ct);
        var trackedDesk = await context.IntakeDesks.AsNoTracking().SingleAsync(d => d.Id == desk.Id, ct);

        // Act
        tracked.ChangeEstimate("S", trackedDesk, scale, IntakeRequestEstimateSource.Manual, IntakeRequestEditor.System, Now)
            .IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);
        tracked.ChangeEstimate("XL", trackedDesk, scale, IntakeRequestEstimateSource.Manual, IntakeRequestEditor.System, Now)
            .IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        var savedDesk = await reader.IntakeDesks.AsNoTracking().SingleAsync(d => d.Id == desk.Id, ct);
        savedDesk.EstimationScaleVersionId.Should().Be(scale.CurrentVersion.Id);
        var saved = await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id, ct);
        saved.Estimate!.Value.Should().Be("XL");
        saved.Estimate.ScaleVersionId.Should().Be(scale.CurrentVersion.Id);
        saved.Estimate.Position.Should().Be(3);
        saved.Estimate.ScaleName.Should().Be(scale.Name);
        saved.Estimate.ScaleVersionNumber.Should().Be(1);

        var payloads = await reader.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == request.Id && a.EventType == nameof(IntakeRequestEstimateChangedEvent))
            .Select(a => a.Payload)
            .ToListAsync(ct);
        payloads.Should().HaveCount(2);
        payloads.Should().ContainSingle(p => p.Contains("\"from\":{\"value\":\"S\"") && p.Contains("\"to\":{\"value\":\"XL\""));
    }

    [Fact]
    public async Task SaveChanges_ClearingAnEstimate_LeavesNoEstimate()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, scale, requester) = await SeedEstimatingDesk(ct);
        await using var context = _fixture.CreateContext();
        var request = await SeedRequest(context, desk, requester, ct, estimate: "M", scale: scale);
        var tracked = await context.IntakeRequests.SingleAsync(r => r.Id == request.Id, ct);

        // Act
        tracked.ChangeEstimate(null, desk, scale, IntakeRequestEstimateSource.Manual, IntakeRequestEditor.System, Now)
            .IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        // Assert
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id, ct)).Estimate.Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_RemovingTheCopyOfAScaleADeskPins_IsRefused()
    {
        // Arrange — the foreign key the sync handler's in-use check steers around
        var ct = TestContext.Current.CancellationToken;
        var (_, scale, _) = await SeedEstimatingDesk(ct);
        await using var context = _fixture.CreateContext();
        var copy = await context.DemandEstimationScales.Include(s => s.Versions).SingleAsync(s => s.Id == scale.Id, ct);

        // Act
        context.DemandEstimationScales.Remove(copy);
        var save = () => context.SaveChangesAsync(ct);

        // Assert
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task IsEstimationScaleUsedByDemand_TranslatesAndFindsAnEstimateOnAnOlderVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, scale, requester) = await SeedEstimatingDesk(ct);
        await using var context = _fixture.CreateContext();
        await SeedRequest(context, desk, requester, ct, estimate: "M", scale: scale);
        var unused = await SeedScale(context, ct);

        // Act
        var used = await new IsEstimationScaleUsedByDemandQueryHandler(context).Handle(new IsEstimationScaleUsedByDemandQuery(scale.Id), ct);
        var notUsed = await new IsEstimationScaleUsedByDemandQueryHandler(context).Handle(new IsEstimationScaleUsedByDemandQuery(unused.Id), ct);

        // Assert
        used.Should().BeTrue();
        notUsed.Should().BeFalse();
    }

    [Fact]
    public async Task ReadQueries_ProjectTheDesksScaleAndTheRequestsEstimate()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, scale, requester) = await SeedEstimatingDesk(ct);
        await using var context = _fixture.CreateContext();
        var request = await SeedRequest(context, desk, requester, ct, estimate: "L", scale: scale);
        var agentId = desk.Members.Single().EmployeeId;

        // Act
        var deskDto = await new GetIntakeDeskQueryHandler(context, Principal(agentId)).Handle(new GetIntakeDeskQuery(new IdOrKey(desk.Key)), ct);
        var details = await new GetIntakeRequestQueryHandler(context, Principal(agentId)).Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), ct);
        var estimates = await new GetIntakeRequestEstimatesQueryHandler(context, Principal(agentId)).Handle(new GetIntakeRequestEstimatesQuery(desk.Id), ct);
        var choices = await new GetIntakeEstimationScaleChoicesQueryHandler(context).Handle(new GetIntakeEstimationScaleChoicesQuery(), ct);

        // Assert
        deskDto!.EstimationScale!.Id.Should().Be(scale.Id);
        deskDto.EstimationScale.VersionId.Should().Be(scale.CurrentVersion.Id);
        deskDto.EstimationScale.IsCurrentVersion.Should().BeTrue();

        details!.CanChangeEstimate.Should().BeTrue();
        details.EstimateValues.Should().Equal("S", "M", "L", "XL");
        details.Substance!.Estimate!.Value.Should().Be("L");
        details.Substance.Estimate.Scale.Name.Should().Be(scale.Name);

        var row = estimates.Should().ContainSingle().Subject;
        row.RequestId.Should().Be(request.Id);
        row.Estimate.Position.Should().Be(2);
        row.Estimate.Scale.VersionNumber.Should().Be(1);

        choices.Should().ContainSingle(c => c.Id == scale.Id).Which.EstimateValues.Should().Equal("S", "M", "L", "XL");
    }

    [Fact]
    public async Task OpenEstimates_CountsEachValueOnTheOpenRequestsAlone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, scale, requester) = await SeedEstimatingDesk(ct);
        await using (var seeding = _fixture.CreateContext())
        {
            await SeedRequest(seeding, desk, requester, ct, estimate: "L", scale: scale);
            await SeedRequest(seeding, desk, requester, ct, estimate: "L", scale: scale);
            await SeedRequest(seeding, desk, requester, ct, estimate: "S", scale: scale);
            await SeedRequest(seeding, desk, requester, ct);
        }
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeDeskOpenEstimatesQueryHandler(context, Administrator(requester.Id))
            .Handle(new GetIntakeDeskOpenEstimatesQuery(desk.Id), ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Select(c => (c.Value, c.RequestCount)).Should().Equal(("S", 1), ("L", 2));
        result.Value.Should().OnlyContain(c => c.Scale.VersionId == scale.CurrentVersion.Id && c.Scale.IsCurrentVersion);
    }

    [Fact]
    public async Task ChangeEstimationScale_ClearingOpenEstimates_SavesTheClearedRequests()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, scale, requester) = await SeedEstimatingDesk(ct);
        IntakeRequest request;
        DemandEstimationScale to;
        await using (var seeding = _fixture.CreateContext())
        {
            request = await SeedRequest(seeding, desk, requester, ct, estimate: "M", scale: scale);
            to = await SeedScale(seeding, ct);
        }
        await using var context = _fixture.CreateContext();
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.GetUserId()).Returns("intake-probe");

        // Act
        var result = await new ChangeIntakeDeskEstimationScaleCommandHandler(context, Administrator(requester.Id), user.Object,
                Clock())
            .Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, to.Id, ScaleChangeEstimateHandling.Clear), ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeDesks.AsNoTracking().SingleAsync(d => d.Id == desk.Id, ct)).EstimationScaleVersionId.Should().Be(to.CurrentVersion.Id);
        (await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id, ct)).Estimate.Should().BeNull();
    }

    private async Task<(IntakeDesk Desk, DemandEstimationScale Scale, Employee Requester)> SeedEstimatingDesk(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var agent = await SeedEmployee(context, ct);
        var requester = await SeedEmployee(context, ct);
        var scale = await SeedScale(context, ct);
        var desk = IntakeDesk.Create(UniqueName("Desk"), "Anything", IntakeDeskVisibility.Open, true, true, true,
            [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)], EventActor.System, Now);
        desk.ChangeEstimationScale(scale, null, [], ScaleChangeEstimateHandling.Keep, EventActor.System, Now).IsSuccess.Should().BeTrue();
        context.IntakeDesks.Add(desk);
        await context.SaveChangesAsync(ct);

        return (desk, scale, requester);
    }

    private static async Task<DemandEstimationScale> SeedScale(WaydDbContext context, CancellationToken ct)
    {
        var version = new EstimationScaleVersionValues(Guid.NewGuid(), 1,
            [new("S", true), new("M", true), new("L", true), new("XL", true), new("?", false)]);
        var scale = new DemandEstimationScale(new EstimationScaleSnapshot(Guid.NewGuid(), 3, UniqueName("Sizes"), true, [version]), Now);
        context.DemandEstimationScales.Add(scale);
        await context.SaveChangesAsync(ct);

        return scale;
    }

    private static async Task<IntakeRequest> SeedRequest(WaydDbContext context, IntakeDesk desk, Employee requester, CancellationToken ct,
        string? estimate = null, DemandEstimationScale? scale = null)
    {
        var request = IntakeRequest.Create("Export invoices", "Finance needs a CSV export", null, requester.Id, requester.Id,
            desk, null, null, null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
        if (estimate is not null)
        {
            request.ChangeEstimate(estimate, desk, scale, IntakeRequestEstimateSource.Manual, IntakeRequestEditor.System, Now)
                .IsSuccess.Should().BeTrue();
        }

        context.IntakeRequests.Add(request);
        await context.SaveChangesAsync(ct);

        return request;
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];

    private static async Task<Employee> SeedEmployee(WaydDbContext context, CancellationToken ct)
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
        await context.SaveChangesAsync(ct);

        return employee;
    }

    private static ICurrentPrincipal Principal(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.IsReadOnly).Returns(false);
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal.Object;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        return clock.Object;
    }

    /// <summary>The Demand Management Admin, who is a Desk Admin on every desk.</summary>
    private static ICurrentPrincipal Administrator(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.IsReadOnly).Returns(false);
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal.Object;
    }
}
