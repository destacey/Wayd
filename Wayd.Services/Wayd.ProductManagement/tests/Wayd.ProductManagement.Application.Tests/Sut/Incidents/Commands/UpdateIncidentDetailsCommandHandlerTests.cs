using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
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
    }

    private void SeedEmployee(Guid id)
    {
        var employee = new PrivateConstructorFaker<Employee>()
            .RuleFor(e => e.Id, id)
            .Generate();
        DbContext.Employees.Add(employee);
    }
}
