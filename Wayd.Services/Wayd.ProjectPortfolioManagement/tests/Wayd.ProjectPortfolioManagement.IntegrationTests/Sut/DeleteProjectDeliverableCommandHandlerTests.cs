using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="DeleteProjectDeliverableCommandHandler"/> against a real SQL Server container.
/// <para>
/// The database does not cascade a deliverable's task links, so the handler must load them for the delete to
/// remove them; left out, the save fails on the foreign key. The in-memory fake cannot tell.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeleteProjectDeliverableCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public DeleteProjectDeliverableCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_ForADeliverableWithLinkedTasks_DeletesItAndItsLinks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct, employeeOwnsThePortfolio: true);

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context, plan.EmployeeId)
                .Handle(new DeleteProjectDeliverableCommand(plan.ProjectId, plan.ReportsId, null), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.ProjectDeliverables.AnyAsync(d => d.Id == plan.ReportsId, ct)).Should().BeFalse();
        (await verify.ProjectDeliverableTasks.AnyAsync(l => l.ProjectDeliverableId == plan.ReportsId, ct)).Should().BeFalse();
        (await verify.ProjectDeliverableTasks.CountAsync(ct)).Should().Be(2, "the portal's links are untouched");
    }

    /// <summary>A handler acting as <paramref name="employeeId"/>, with no PPM administrator standing.</summary>
    private static DeleteProjectDeliverableCommandHandler Handler(WaydDbContext context, Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        return new DeleteProjectDeliverableCommandHandler(
            context, principal.Object, currentUser.Object, ActiveProjectWithDeliverable.Clock());
    }
}
