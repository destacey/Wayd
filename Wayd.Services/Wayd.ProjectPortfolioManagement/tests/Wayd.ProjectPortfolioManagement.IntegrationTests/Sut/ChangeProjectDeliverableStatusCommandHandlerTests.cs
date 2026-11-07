using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="ChangeProjectDeliverableStatusCommandHandler"/> against a real SQL Server
/// container.
/// <para>
/// These pin that a deliverable command loads the project's ancestry roles. A portfolio's owner may change its
/// projects' deliverables without a role on the project, which the project can only see when the handler
/// includes the portfolio and its roles; left out, they read as empty and the owner is refused. The in-memory
/// fake treats <c>.Include</c> as a no-op, so a unit test passes either way.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ChangeProjectDeliverableStatusCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ChangeProjectDeliverableStatusCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenTheActorLeadsOnlyThePortfolio_MovesTheDeliverable()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct, employeeOwnsThePortfolio: true);
        var inProgress = seed.Statuses[ProjectDeliverableStatusAlias.InProgress];

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context, seed.EmployeeId)
                .Handle(new ChangeProjectDeliverableStatusCommand(seed.ProjectId, seed.DeliverableId, inProgress.StatusId, null, null), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.ProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct)).StatusId.Should().Be(inProgress.StatusId);
    }

    [Fact]
    public async Task Handle_WhenTheActorLeadsNothing_Refuses()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct);
        var inProgress = seed.Statuses[ProjectDeliverableStatusAlias.InProgress];

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context, seed.EmployeeId)
                .Handle(new ChangeProjectDeliverableStatusCommand(seed.ProjectId, seed.DeliverableId, inProgress.StatusId, null, null), ct);
        }

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");

        await using var verify = _fixture.CreateContext();
        (await verify.ProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct))
            .StatusId.Should().Be(seed.Statuses[ProjectDeliverableStatusAlias.NotStarted].StatusId);
    }

    /// <summary>A handler acting as <paramref name="employeeId"/>, with no PPM administrator standing.</summary>
    private static ChangeProjectDeliverableStatusCommandHandler Handler(WaydDbContext context, Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        return new ChangeProjectDeliverableStatusCommandHandler(
            context,
            principal.Object,
            currentUser.Object,
            new StatusResolver(context),
            ActiveProjectWithDeliverable.Clock());
    }
}
