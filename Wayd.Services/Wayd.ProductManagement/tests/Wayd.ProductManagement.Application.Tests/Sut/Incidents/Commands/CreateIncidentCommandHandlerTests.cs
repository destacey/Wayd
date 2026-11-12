using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Commands;

public sealed class CreateIncidentCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public CreateIncidentCommandHandlerTests()
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

    private CreateIncidentCommandHandler CreateSut() =>
        new(
            DbContext,
            _statusResolver.Object,
            CurrentUser.Object,
            CurrentPrincipal.Object,
            DateTimeProvider.Object,
            Logger<CreateIncidentCommandHandler>());

    [Fact]
    public async Task Handle_ShouldRecordWhoOpenedItSeparatelyFromWhoReportedIt()
    {
        // Arrange
        var set = SeedSystemSeveritySet();
        SeedOrganizationSelection(set);
        var level = set.Levels.First();
        var product = SeedProduct();
        SeedEmployee(ActingEmployeeId);
        var reportedById = Guid.CreateVersion7();
        SeedEmployee(reportedById);
        var commanderId = Guid.CreateVersion7();
        SeedEmployee(commanderId);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateIncidentCommand(
                "API errors",
                "Elevated 5xx rate",
                "Rolled back the deploy",
                null,
                level.Id,
                NoticedAt: null,
                StartedAt: null,
                RecoveredAt: null,
                [product.Id],
                reportedById,
                commanderId),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var incident = DbContext.Incidents.Should().ContainSingle().Subject;
        incident.SubmittedByEmployeeId.Should().Be(ActingEmployeeId);
        incident.ReportedByEmployeeId.Should().Be(reportedById);
        incident.CommanderEmployeeId.Should().Be(commanderId);
        incident.SeverityLevelId.Should().Be(level.Id);
        incident.Remediation.Should().Be("Rolled back the deploy");
        incident.NoticedAt.Should().Be(Now);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSeverityLevelIsNotInTheSelectedSet()
    {
        // Arrange
        var selected = SeedSystemSeveritySet("Selected");
        var other = SeedCustomSeveritySet("Other");
        SeedOrganizationSelection(selected);
        var level = other.Levels.First();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateIncidentCommand("Issue", null, null, null, level.Id, null, null, null, [], Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.Incidents.Should().BeEmpty();
    }

    private void SeedEmployee(Guid id)
    {
        var employee = new PrivateConstructorFaker<Employee>()
            .RuleFor(e => e.Id, id)
            .Generate();
        DbContext.Employees.Add(employee);
    }
}
