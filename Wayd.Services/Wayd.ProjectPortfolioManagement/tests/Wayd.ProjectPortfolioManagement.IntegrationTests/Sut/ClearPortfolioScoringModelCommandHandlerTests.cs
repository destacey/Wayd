using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Scoring.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="ClearPortfolioScoringModelCommandHandler"/> against a real SQL Server
/// container.
/// <para>
/// The event names the model being cleared, which the handler can only know if its query loads
/// <c>ProjectPortfolio.ScoringModel</c>. Against the in-memory fake, <c>.Include</c> is a no-op and the test
/// sets the navigation by hand, so the unit test cannot tell whether the query loads it.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ClearPortfolioScoringModelCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ClearPortfolioScoringModelCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_ClearingTheModel_NamesTheModelItClearedWithoutWritingToIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await Seed(ct);
        var recorder = new SavedEntityRecorder();

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(recorder))
        {
            result = await Handler(context, seed.EmployeeId)
                .Handle(new ClearPortfolioScoringModelCommand(seed.PortfolioId), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        recorder.Writes.Should().NotContain(w => w.Type.Namespace == typeof(ScoringModel).Namespace,
            "clearing the portfolio's model must not touch the model itself");

        await using var verify = _fixture.CreateContext();
        (await verify.Portfolios.SingleAsync(p => p.Id == seed.PortfolioId, ct)).ScoringModelId.Should().BeNull();
        (await verify.ScoringModels.CountAsync(ct)).Should().Be(1);

        // Selected by actor, not by time: the seed raises its own scoring-model event, attributed to the
        // system, and ActivityLogs has no ordering key — both entries carry FixedNow, so ordering by
        // Timestamp picks either.
        var entry = await verify.ActivityLogs
            .SingleAsync(a => a.AggregateId == seed.PortfolioId
                && a.EventType == nameof(ProjectPortfolioScoringModelChangedEvent)
                && a.ActorKind == EventActorKind.User, ct);
        var payload = JsonDocument.Parse(entry.Payload).RootElement;
        payload.GetProperty("previousScoringModelId").GetGuid().Should().Be(seed.Model.Id);
        payload.GetProperty("previousScoringModelName").GetString().Should().Be(seed.Model.Name,
            "the name has to come from the model the query loaded");
        payload.GetProperty("scoringModelId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// Seeds an employee to act as and an active portfolio already scoring against an active model, both
    /// written through EF exactly as production writes them.
    /// </summary>
    private async Task<(Guid PortfolioId, Guid EmployeeId, ScoringModel Model)> Seed(CancellationToken cancellationToken)
    {
        await _fixture.ResetPpmData(cancellationToken);
        await using var context = _fixture.CreateContext();

        var employee = Employee.Create(
            new PersonName("Ada", null, "Lovelace"),
            "E1000",
            hireDate: SqlServerDbContextFixture.FixedNow,
            new EmailAddress("ada.lovelace@acme.example"),
            jobTitle: "Delivery Lead",
            department: "Engineering",
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            SqlServerDbContextFixture.FixedNow);
        await context.Employees.AddAsync(employee, cancellationToken);

        // Key is an identity column, so the faker's random value is cleared for SQL Server to assign.
        var model = new ScoringModelFaker().WithKey(0).AsActiveWsjf();
        await context.ScoringModels.AddAsync(model, cancellationToken);

        var portfolio = ProjectPortfolio.Create(
            "Delivery", "Delivery portfolio", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
        portfolio.Activate(PpmActor.System, SqlServerDbContextFixture.FixedNow.InUtc().Date, SqlServerDbContextFixture.FixedNow);
        portfolio.AssignScoringModel(model, PpmActor.System, SqlServerDbContextFixture.FixedNow);

        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return (portfolio.Id, employee.Id, model);
    }

    private static ClearPortfolioScoringModelCommandHandler Handler(WaydDbContext context, Guid employeeId)
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

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        dateTimeProvider.SetupGet(d => d.Today).Returns(SqlServerDbContextFixture.FixedNow.InUtc().Date);

        return new ClearPortfolioScoringModelCommandHandler(
            context,
            principal.Object,
            currentUser.Object,
            Mock.Of<ILogger<ClearPortfolioScoringModelCommandHandler>>(),
            dateTimeProvider.Object);
    }
}
