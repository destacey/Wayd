using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="CompleteProjectCommandHandler"/> against a real SQL Server container.
/// <para>
/// These need the container because the rule they pin is a loading question. A project refuses to complete
/// while a deliverable is open, and it can only see its deliverables if the handler includes them; left out,
/// the collection reads as empty and the project completes. The in-memory fake treats <c>.Include</c> as a
/// no-op and always has the deliverables, so a unit test passes either way.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class CompleteProjectCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public CompleteProjectCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WithAnOpenDeliverable_RefusesAndLeavesTheProjectActive()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context, seed.EmployeeId).Handle(new CompleteProjectCommand(seed.ProjectId), ct);
        }

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A deliverable is still open. Accept or descope it before completing the project.");

        await using var verify = _fixture.CreateContext();
        (await verify.Projects.SingleAsync(p => p.Id == seed.ProjectId, ct)).Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public async Task Handle_WithEveryDeliverableAccepted_CompletesTheProject()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: true, ct);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context, seed.EmployeeId).Handle(new CompleteProjectCommand(seed.ProjectId), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.Projects.SingleAsync(p => p.Id == seed.ProjectId, ct)).Status.Should().Be(ProjectStatus.Completed);
    }

    private static CompleteProjectCommandHandler Handler(WaydDbContext context, Guid employeeId)
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

        return new CompleteProjectCommandHandler(
            context,
            principal.Object,
            currentUser.Object,
            ActiveProjectWithDeliverable.Clock(),
            Mock.Of<ILogger<CompleteProjectCommandHandler>>());
    }
}
