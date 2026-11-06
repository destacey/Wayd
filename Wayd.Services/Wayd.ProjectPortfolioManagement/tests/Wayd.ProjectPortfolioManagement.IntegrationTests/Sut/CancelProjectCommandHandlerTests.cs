using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="CancelProjectCommandHandler"/> against a real SQL Server container.
/// <para>
/// Canceling descopes every open deliverable, which the project can only do for the deliverables the handler
/// loads; left out, the collection reads as empty and they stay open on a canceled project. The in-memory fake
/// treats <c>.Include</c> as a no-op, so only a real database shows the difference.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class CancelProjectCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public CancelProjectCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WithAnOpenDeliverable_CancelsAndDescopesIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context, seed.EmployeeId).Handle(new CancelProjectCommand(seed.ProjectId), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.Projects.SingleAsync(p => p.Id == seed.ProjectId, ct)).Status.Should().Be(ProjectStatus.Canceled);

        var deliverable = await verify.ProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct);
        deliverable.StatusId.Should().Be(seed.Statuses[ProjectDeliverableStatusAlias.Descoped].StatusId);
        deliverable.StatusReason.Should().Be(Project.CanceledDeliverableReason);
        deliverable.IsOpen.Should().BeFalse();

        var transitions = await verify.StatusTransitions
            .Where(t => t.RecordId == seed.DeliverableId)
            .OrderBy(t => t.Sequence)
            .ToListAsync(ct);
        var descoped = transitions.Last();
        descoped.ToStatusId.Should().Be(seed.Statuses[ProjectDeliverableStatusAlias.Descoped].StatusId);
        descoped.Reason.Should().Be(Project.CanceledDeliverableReason);
    }

    [Fact]
    public async Task Handle_WithEveryDeliverableAccepted_LeavesThemAccepted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: true, ct);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context, seed.EmployeeId).Handle(new CancelProjectCommand(seed.ProjectId), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.ProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct))
            .StatusId.Should().Be(seed.Statuses[ProjectDeliverableStatusAlias.Accepted].StatusId);
    }

    private static CancelProjectCommandHandler Handler(WaydDbContext context, Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);
        principal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        return new CancelProjectCommandHandler(
            context,
            principal.Object,
            currentUser.Object,
            ActiveProjectWithDeliverable.Clock(),
            new StatusResolver(context),
            Mock.Of<ILogger<CancelProjectCommandHandler>>());
    }
}
