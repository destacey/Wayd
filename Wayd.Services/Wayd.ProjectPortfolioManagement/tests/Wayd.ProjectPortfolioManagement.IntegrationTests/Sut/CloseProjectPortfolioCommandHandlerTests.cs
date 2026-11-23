using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="CloseProjectPortfolioCommandHandler"/> against a real SQL Server container.
/// <para>
/// These need the container because the rule they pin is a loading question. A portfolio refuses to close while a
/// program or project is open, and it can only see them if the handler includes them; left out, the collections
/// read as empty and the portfolio closes. The in-memory fake treats <c>.Include</c> as a no-op, so a unit test
/// passes either way.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class CloseProjectPortfolioCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public CloseProjectPortfolioCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WithAnOpenProgram_RefusesAndLeavesThePortfolioActive()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActivePortfolioWithProgram.Seed(_fixture, cancelTheProject: true, cancelTheProgram: false, ct);

        // Act
        var result = await Close(seed, ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All programs must be completed or canceled before the portfolio can be closed.");

        await using var verify = _fixture.CreateContext();
        (await verify.Portfolios.SingleAsync(p => p.Id == seed.PortfolioId, ct)).Status.Should().Be(ProjectPortfolioStatus.Active);
    }

    [Fact]
    public async Task Handle_WithAnOpenProject_RefusesAndLeavesThePortfolioActive()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActivePortfolioWithProgram.Seed(
            _fixture, cancelTheProject: false, cancelTheProgram: true, ct, projectInProgram: false);

        // Act
        var result = await Close(seed, ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All projects must be completed or canceled before the portfolio can be closed.");

        await using var verify = _fixture.CreateContext();
        (await verify.Portfolios.SingleAsync(p => p.Id == seed.PortfolioId, ct)).Status.Should().Be(ProjectPortfolioStatus.Active);
    }

    [Fact]
    public async Task Handle_WithEveryChildClosed_ClosesThePortfolio()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActivePortfolioWithProgram.Seed(_fixture, cancelTheProject: true, cancelTheProgram: true, ct);

        // Act
        var result = await Close(seed, ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.Portfolios.SingleAsync(p => p.Id == seed.PortfolioId, ct)).Status.Should().Be(ProjectPortfolioStatus.Closed);
    }

    private async Task<Result> Close(ActivePortfolioWithProgram seed, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await Handler(context, seed.EmployeeId).Handle(new CloseProjectPortfolioCommand(seed.PortfolioId), ct);
    }

    private static CloseProjectPortfolioCommandHandler Handler(WaydDbContext context, Guid employeeId)
    {
        var clock = ActiveProjectWithDeliverable.Clock();
        return new CloseProjectPortfolioCommandHandler(
            context,
            PpmAdministrator.Principal(employeeId),
            PpmAdministrator.User(),
            Mock.Of<ILogger<CloseProjectPortfolioCommandHandler>>(),
            clock,
            new TestingSystemCalendar(clock));
    }
}
