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
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Commands;

public sealed class LinkIncidentTeamsChatCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    [Fact]
    public async Task Handle_ShouldStoreThePastedLink()
    {
        // Arrange
        var incident = await OpenIncident();
        var savesBefore = DbContext.SaveChangesCallCount;
        var handler = new LinkIncidentTeamsChatCommandHandler(
            DbContext,
            CurrentUser.Object,
            CurrentPrincipal.Object,
            DateTimeProvider.Object,
            Logger<LinkIncidentTeamsChatCommandHandler>());

        // Act
        var result = await handler.Handle(
            new LinkIncidentTeamsChatCommand(
                incident.Id,
                " https://teams.microsoft.com/l/chat/19%3Aabc/0 ",
                " Bridge "),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.TeamsChat!.Link.Should().Be("https://teams.microsoft.com/l/chat/19%3Aabc/0");
        incident.TeamsChat.Title.Should().Be("Bridge");
        incident.TeamsChat.CreatedByWayd.Should().BeFalse();
        incident.TeamsChat.ExternalId.Should().BeNull();
        incident.TeamsChat.Connector.Should().Be(Wayd.Common.Domain.Enums.AppIntegrations.Connector.Teams);
        DbContext.SaveChangesCallCount.Should().Be(savesBefore + 1);
    }

    [Fact]
    public async Task Handle_ShouldNotSave_WhenTheLinkIsUnchanged()
    {
        // Arrange
        var incident = await OpenIncident();
        var handler = new LinkIncidentTeamsChatCommandHandler(
            DbContext,
            CurrentUser.Object,
            CurrentPrincipal.Object,
            DateTimeProvider.Object,
            Logger<LinkIncidentTeamsChatCommandHandler>());
        await handler.Handle(
            new LinkIncidentTeamsChatCommand(incident.Id, "https://teams.microsoft.com/l/chat/19%3Aabc/0", "Bridge"),
            TestContext.Current.CancellationToken);
        var savesAfterFirstLink = DbContext.SaveChangesCallCount;

        // Act
        var result = await handler.Handle(
            new LinkIncidentTeamsChatCommand(incident.Id, "https://teams.microsoft.com/l/chat/19%3Aabc/0", "Bridge"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(savesAfterFirstLink);
    }

    [Fact]
    public async Task Handle_ShouldClearTheStoredChat_WhenTheLinkIsEmpty()
    {
        // Arrange
        var incident = await OpenIncident();
        var handler = new LinkIncidentTeamsChatCommandHandler(
            DbContext,
            CurrentUser.Object,
            CurrentPrincipal.Object,
            DateTimeProvider.Object,
            Logger<LinkIncidentTeamsChatCommandHandler>());
        await handler.Handle(
            new LinkIncidentTeamsChatCommand(incident.Id, "https://teams.microsoft.com/l/chat/19%3Aabc/0", "Bridge"),
            TestContext.Current.CancellationToken);
        var savesAfterLink = DbContext.SaveChangesCallCount;

        // Act
        var result = await handler.Handle(
            new LinkIncidentTeamsChatCommand(incident.Id, "", "Bridge"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.TeamsChat.Should().BeNull();
        DbContext.SaveChangesCallCount.Should().Be(savesAfterLink + 1);
    }

    [Fact]
    public async Task Handle_ShouldNotSave_WhenTheChatIsAlreadyClear()
    {
        // Arrange
        var incident = await OpenIncident();
        var savesBefore = DbContext.SaveChangesCallCount;
        var handler = new LinkIncidentTeamsChatCommandHandler(
            DbContext,
            CurrentUser.Object,
            CurrentPrincipal.Object,
            DateTimeProvider.Object,
            Logger<LinkIncidentTeamsChatCommandHandler>());

        // Act
        var result = await handler.Handle(
            new LinkIncidentTeamsChatCommand(incident.Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.TeamsChat.Should().BeNull();
        DbContext.SaveChangesCallCount.Should().Be(savesBefore);
    }

    private async Task<Incident> OpenIncident()
    {
        var set = SeedSystemSeveritySet();
        SeedOrganizationSelection(set);
        var level = set.Levels.First();
        SeedEmployee(ActingEmployeeId);
        var reportedById = Guid.CreateVersion7();
        SeedEmployee(reportedById);
        var opened = await new CreateIncidentCommandHandler(
                DbContext,
                StatusResolver().Object,
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
        opened.IsSuccess.Should().BeTrue();
        return DbContext.Incidents.Should().ContainSingle().Subject;
    }

    private Mock<IStatusResolver> StatusResolver()
    {
        var resolver = new Mock<IStatusResolver>();
        resolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Incident.Key,
                null,
                (int)ProductStatusAlias.Open,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Open", StatusCategory.Active, ProductStatusAlias.Open)));
        return resolver;
    }

    private void SeedEmployee(Guid id)
    {
        var employee = new PrivateConstructorFaker<Employee>()
            .RuleFor(e => e.Id, id)
            .Generate();
        DbContext.Employees.Add(employee);
    }
}
