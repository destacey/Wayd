using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="ChangeProjectLifecycleCommandHandler"/> against a real SQL Server
/// container.
/// <para>
/// These need the container because the handler's correctness is a change-tracking question. The incoming
/// lifecycle becomes the project's navigation, and <c>ProjectLifecycle.Id</c> is never generated, so a
/// lifecycle EF did not already track would be inserted as a new row. The lifecycle being replaced is read
/// through <c>Include</c> so the event can name it. The in-memory fake models neither, so a unit test passes
/// either way.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ChangeProjectLifecycleCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ChangeProjectLifecycleCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_ChangingTheLifecycle_NamesBothLifecyclesWithoutWritingToEither()
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
                .Handle(new ChangeProjectLifecycleCommand(seed.ProjectId, seed.Replacement.Id, []), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        recorder.Writes.Should().Contain(w => w.Type == typeof(Project), "the project's lifecycle is the write");
        recorder.Writes.Should().NotContain(w => w.Type == typeof(ProjectLifecycle) || w.Type == typeof(ProjectLifecycleStage),
            "neither the replaced lifecycle nor the incoming one may be inserted or rewritten");

        await using var verify = _fixture.CreateContext();
        (await verify.Projects.SingleAsync(p => p.Id == seed.ProjectId, ct)).ProjectLifecycleId.Should().Be(seed.Replacement.Id);
        (await verify.ProjectLifecycles.CountAsync(ct)).Should().Be(2);

        var entry = await verify.ActivityLogs
            .SingleAsync(a => a.AggregateId == seed.ProjectId
                && a.EventType == nameof(ProjectLifecycleChangedEventV2), ct);
        var payload = JsonDocument.Parse(entry.Payload).RootElement;
        payload.GetProperty("previousLifecycleId").GetGuid().Should().Be(seed.Replaced.Id);
        payload.GetProperty("previousLifecycleName").GetString().Should().Be(seed.Replaced.Name,
            "the name has to come from the lifecycle the query loaded, not from a navigation left empty");
        payload.GetProperty("lifecycleId").GetGuid().Should().Be(seed.Replacement.Id);
        payload.GetProperty("lifecycleName").GetString().Should().Be(seed.Replacement.Name);
    }

    /// <summary>
    /// Seeds an employee to act as, two active lifecycles, and a project in an active portfolio following the
    /// first of them, all written through EF exactly as production writes them.
    /// </summary>
    private async Task<(Guid ProjectId, Guid EmployeeId, ProjectLifecycle Replaced, ProjectLifecycle Replacement)> Seed(
        CancellationToken cancellationToken)
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

        var category = ExpenditureCategory.Create("Capital", "Capital spend", isCapitalizable: true, requiresDepreciation: true);
        await context.ExpenditureCategories.AddAsync(category, cancellationToken);

        var replaced = ProjectLifecycle.Create("Standard", "Standard delivery lifecycle", [("Delivery", "Delivery stage")]);
        replaced.Activate();
        var replacement = ProjectLifecycle.Create("Accelerated", "Accelerated delivery lifecycle", [("Build", "Build stage")]);
        replacement.Activate();
        await context.ProjectLifecycles.AddRangeAsync([replaced, replacement], cancellationToken);

        var portfolio = ProjectPortfolio.Create(
            "Delivery", "Delivery portfolio", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        portfolio.Activate(PpmActor.System, SqlServerDbContextFixture.FixedNow.InUtc().Date, SqlServerDbContextFixture.FixedNow);
        var project = portfolio.CreateProject(
            "Gemini",
            "Gemini description",
            new ProjectKey("GEMINI"),
            category.Id,
            dateRange: null,
            programId: null,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: null,
            SqlServerDbContextFixture.FixedNow,
            PpmActor.System).Value;
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, replaced, SqlServerDbContextFixture.FixedNow);
        await context.SaveChangesAsync(cancellationToken);

        return (project.Id, employee.Id, replaced, replacement);
    }

    private static ChangeProjectLifecycleCommandHandler Handler(WaydDbContext context, Guid employeeId)
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

        return new ChangeProjectLifecycleCommandHandler(
            context,
            principal.Object,
            currentUser.Object,
            dateTimeProvider.Object,
            Mock.Of<ILogger<ChangeProjectLifecycleCommandHandler>>());
    }
}
