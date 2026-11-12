using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Commands;

public sealed class UpdateIncidentDetailsCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public UpdateIncidentDetailsCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Incident.Key,
                null,
                (int)ProductStatusAlias.Open,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Open", StatusCategory.Active, ProductStatusAlias.Open)));
    }

    [Fact]
    public async Task Handle_ShouldStoreRemediation()
    {
        // Arrange
        var set = SeedSystemSeveritySet();
        SeedOrganizationSelection(set);
        var level = set.Levels.First();
        SeedEmployee(ActingEmployeeId);
        var reportedById = Guid.CreateVersion7();
        SeedEmployee(reportedById);
        var opened = await new CreateIncidentCommandHandler(
                DbContext,
                _statusResolver.Object,
                CurrentUser.Object,
                CurrentPrincipal.Object,
                DateTimeProvider.Object,
                Logger<CreateIncidentCommandHandler>())
            .Handle(
                new CreateIncidentCommand(
                    "API errors",
                    "Elevated 5xx rate",
                    null,
                    null,
                    level.Id,
                    null,
                    null,
                    null,
                    [],
                    reportedById),
                TestContext.Current.CancellationToken);
        var incident = DbContext.Incidents.Should().ContainSingle().Subject;
        var savesBeforeUpdate = DbContext.SaveChangesCallCount;

        // Act
        var result = await new UpdateIncidentDetailsCommandHandler(
                DbContext,
                CurrentUser.Object,
                CurrentPrincipal.Object,
                DateTimeProvider.Object,
                Logger<UpdateIncidentDetailsCommandHandler>())
            .Handle(
                new UpdateIncidentDetailsCommand(
                    incident.Id,
                    "API errors",
                    "Elevated 5xx rate",
                    "Rolled back the deploy",
                    null,
                    level.Id,
                    incident.NoticedAt,
                    null,
                    null,
                    [],
                    reportedById),
                TestContext.Current.CancellationToken);

        // Assert
        opened.IsSuccess.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        incident.Remediation.Should().Be("Rolled back the deploy");
        DbContext.SaveChangesCallCount.Should().Be(savesBeforeUpdate + 1);
    }

    [Fact]
    public async Task Handle_ShouldSayMembersWereNotAdded_WhenWaydCreatedTheChat_AndReportedByChanges()
    {
        // Arrange
        var set = SeedSystemSeveritySet();
        SeedOrganizationSelection(set);
        var level = set.Levels.First();
        SeedEmployee(ActingEmployeeId);
        var reportedById = Guid.CreateVersion7();
        SeedEmployee(reportedById);
        var nextReportedById = Guid.CreateVersion7();
        SeedEmployee(nextReportedById);
        var opened = await new CreateIncidentCommandHandler(
                DbContext,
                _statusResolver.Object,
                CurrentUser.Object,
                CurrentPrincipal.Object,
                DateTimeProvider.Object,
                Logger<CreateIncidentCommandHandler>())
            .Handle(
                new CreateIncidentCommand(
                    "API errors",
                    "Elevated 5xx rate",
                    null,
                    null,
                    level.Id,
                    null,
                    null,
                    null,
                    [],
                    reportedById),
                TestContext.Current.CancellationToken);
        var incident = DbContext.Incidents.Should().ContainSingle().Subject;
        typeof(Incident).GetProperty(nameof(Incident.TeamsChatCreatedByWayd))!
            .SetValue(incident, true);
        var logger = new Mock<ILogger<UpdateIncidentDetailsCommandHandler>>();

        // Act
        var result = await new UpdateIncidentDetailsCommandHandler(
                DbContext,
                CurrentUser.Object,
                CurrentPrincipal.Object,
                DateTimeProvider.Object,
                logger.Object)
            .Handle(
                new UpdateIncidentDetailsCommand(
                    incident.Id,
                    "API errors",
                    "Elevated 5xx rate",
                    null,
                    null,
                    level.Id,
                    incident.NoticedAt,
                    null,
                    null,
                    [],
                    nextReportedById),
                TestContext.Current.CancellationToken);

        // Assert
        opened.IsSuccess.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        incident.ReportedByEmployeeId.Should().Be(nextReportedById);
        incident.TeamsChatCreatedByWayd.Should().BeTrue();
        logger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => (Convert.ToString(value) ?? string.Empty).Contains(IncidentTeamsMembership.MembersNotAdded)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void SeedEmployee(Guid id)
    {
        var employee = new PrivateConstructorFaker<Employee>()
            .RuleFor(e => e.Id, id)
            .Generate();
        DbContext.Employees.Add(employee);
    }
}
