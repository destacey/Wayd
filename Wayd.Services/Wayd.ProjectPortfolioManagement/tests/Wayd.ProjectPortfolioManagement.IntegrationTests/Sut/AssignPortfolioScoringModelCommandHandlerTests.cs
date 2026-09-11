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
/// Integration tests for <see cref="AssignPortfolioScoringModelCommandHandler"/> against a real SQL Server
/// container.
/// <para>
/// These need the container because the handler's correctness is a change-tracking question. It loads the
/// incoming model tracked and hands it to the portfolio as its navigation, and it loads the model being
/// replaced through <c>Include</c> so the event can name it. <c>ScoringModel.Id</c> is never generated, so
/// an untracked model reached through that navigation would be inserted as a new row, and a model EF
/// wrongly thought changed would be rewritten. The in-memory fake models neither, so a unit test passes
/// either way.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class AssignPortfolioScoringModelCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public AssignPortfolioScoringModelCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_AssigningTheFirstModel_PersistsItWithoutWritingToTheModel()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await Seed(assignFirst: false, ct);
        var recorder = new SavedEntityRecorder();

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(recorder))
        {
            result = await Handler(context, seed.EmployeeId)
                .Handle(new AssignPortfolioScoringModelCommand(seed.PortfolioId, seed.First.Id), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        recorder.Writes.Should().Contain(w => w.Type == typeof(ProjectPortfolio), "the portfolio's assignment is the write");
        recorder.Writes.Should().NotContain(w => w.Type.Namespace == typeof(ScoringModel).Namespace,
            "assigning a model must neither insert it nor rewrite it");

        await using var verify = _fixture.CreateContext();
        (await verify.Portfolios.SingleAsync(p => p.Id == seed.PortfolioId, ct)).ScoringModelId.Should().Be(seed.First.Id);
        (await verify.ScoringModels.CountAsync(ct)).Should().Be(2);

        var payload = await ScoringModelChangedPayload(verify, seed.PortfolioId, ct);
        payload.GetProperty("previousScoringModelId").ValueKind.Should().Be(JsonValueKind.Null);
        payload.GetProperty("scoringModelId").GetGuid().Should().Be(seed.First.Id);
        payload.GetProperty("scoringModelName").GetString().Should().Be(seed.First.Name);
    }

    [Fact]
    public async Task Handle_ReplacingAModel_NamesBothModelsWithoutWritingToEither()
    {
        // Arrange — the replaced model is loaded through Include(p => p.ScoringModel), the incoming one
        // tracked on its own, so both are in the change tracker when the portfolio saves
        var ct = TestContext.Current.CancellationToken;
        var seed = await Seed(assignFirst: true, ct);
        var recorder = new SavedEntityRecorder();

        // Act
        Result result;
        await using (var context = _fixture.CreateContext(recorder))
        {
            result = await Handler(context, seed.EmployeeId)
                .Handle(new AssignPortfolioScoringModelCommand(seed.PortfolioId, seed.Second.Id), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        recorder.Writes.Should().NotContain(w => w.Type.Namespace == typeof(ScoringModel).Namespace,
            "neither the replaced model nor the incoming one may be inserted or rewritten");

        await using var verify = _fixture.CreateContext();
        (await verify.Portfolios.SingleAsync(p => p.Id == seed.PortfolioId, ct)).ScoringModelId.Should().Be(seed.Second.Id);
        (await verify.ScoringModels.CountAsync(ct)).Should().Be(2);

        var payload = await ScoringModelChangedPayload(verify, seed.PortfolioId, ct);
        payload.GetProperty("previousScoringModelId").GetGuid().Should().Be(seed.First.Id);
        payload.GetProperty("previousScoringModelName").GetString().Should().Be(seed.First.Name,
            "the name has to come from the model the query loaded, not from a navigation left empty");
        payload.GetProperty("scoringModelId").GetGuid().Should().Be(seed.Second.Id);
        payload.GetProperty("scoringModelName").GetString().Should().Be(seed.Second.Name);
    }

    /// <summary>
    /// Seeds an employee to act as, two active scoring models written through EF exactly as production
    /// writes them, and an active portfolio — optionally already scoring against the first model.
    /// </summary>
    private async Task<(Guid PortfolioId, Guid EmployeeId, ScoringModel First, ScoringModel Second)> Seed(
        bool assignFirst, CancellationToken cancellationToken)
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
        var first = new ScoringModelFaker().WithKey(0).AsActiveWsjf();
        var second = new ScoringModelFaker().WithKey(0).AsActiveWsjf();
        await context.ScoringModels.AddRangeAsync([first, second], cancellationToken);

        var portfolio = ProjectPortfolio.Create(
            "Delivery", "Delivery portfolio", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
        portfolio.Activate(PpmActor.System, SqlServerDbContextFixture.FixedNow.InUtc().Date, SqlServerDbContextFixture.FixedNow);
        if (assignFirst)
        {
            portfolio.AssignScoringModel(first, PpmActor.System, SqlServerDbContextFixture.FixedNow);
        }

        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return (portfolio.Id, employee.Id, first, second);
    }

    private static AssignPortfolioScoringModelCommandHandler Handler(WaydDbContext context, Guid employeeId)
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

        return new AssignPortfolioScoringModelCommandHandler(
            context,
            principal.Object,
            currentUser.Object,
            Mock.Of<ILogger<AssignPortfolioScoringModelCommandHandler>>(),
            dateTimeProvider.Object);
    }

    private static async Task<JsonElement> ScoringModelChangedPayload(
        WaydDbContext context, Guid portfolioId, CancellationToken cancellationToken)
    {
        // Selected by actor, not by time: the seed raises its own scoring-model event, attributed to the
        // system, and ActivityLogs has no ordering key — both entries carry FixedNow, so ordering by
        // Timestamp picks either.
        var entry = await context.ActivityLogs
            .SingleAsync(a => a.AggregateId == portfolioId
                && a.EventType == nameof(ProjectPortfolioScoringModelChangedEvent)
                && a.ActorKind == EventActorKind.User, cancellationToken);

        return JsonDocument.Parse(entry.Payload).RootElement.Clone();
    }
}
