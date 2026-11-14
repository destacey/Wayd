using System.Reflection;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;
using Wayd.Common.Application.Requests.AppIntegration;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement.Incidents;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Commands;

public sealed class CreateIncidentTeamsChatCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private readonly Mock<IDispatcher> _dispatcher = new();

    [Fact]
    public async Task Handle_ShouldStoreTheChatGraphCreated()
    {
        // Arrange
        var incident = await OpenIncident();
        var extraId = Guid.CreateVersion7();
        SeedEmployee(extraId);
        SetEmail(incident.ReportedByEmployeeId, "reported@contoso.com");
        SetEmail(incident.SubmittedByEmployeeId, "submitted@contoso.com");
        var commanderId = Guid.CreateVersion7();
        SeedEmployee(commanderId);
        SetEmail(commanderId, "commander@contoso.com");
        SetCommander(incident, commanderId);
        SetKey(incident, 42);
        SetEmail(extraId, "extra@contoso.com");
        var savesBefore = DbContext.SaveChangesCallCount;
        CreateTeamsGroupChatCommand? sent = null;
        _dispatcher
            .Setup(d => d.Send(It.IsAny<CreateTeamsGroupChatCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<TeamsCreatedChat>, CancellationToken>((command, _) => sent = (CreateTeamsGroupChatCommand)command)
            .ReturnsAsync(Result.Success(new TeamsCreatedChat(
                "19:abc@thread.v2",
                "https://teams.microsoft.com/l/chat/19%3Aabc/0")));
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new CreateIncidentTeamsChatCommand(incident.Id, [extraId]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        sent.Should().NotBeNull();
        sent!.Topic.Should().Be("INC-42 API errors");
        sent.OptionalMemberEmails.Should().Equal(
            "reported@contoso.com",
            "submitted@contoso.com",
            "commander@contoso.com");
        sent.RequiredMemberEmails.Should().Equal("extra@contoso.com");
        incident.TeamsChat!.ExternalId.Should().Be("19:abc@thread.v2");
        incident.TeamsChat.Title.Should().Be("INC-42 API errors");
        incident.TeamsChat.Link.Should().Be("https://teams.microsoft.com/l/chat/19%3Aabc/0");
        incident.TeamsChat.CreatedByWayd.Should().BeTrue();
        incident.DomainEvents.OfType<IncidentTeamsChatCreatedEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(savesBefore + 1);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheKeyInATopicThatGraphWouldReject()
    {
        // Arrange
        var incident = await OpenIncident();
        SetKey(incident, 42);
        SetTitle(incident, new string('a', 256));
        SetEmail(incident.ReportedByEmployeeId, "reported@contoso.com");
        CreateTeamsGroupChatCommand? sent = null;
        _dispatcher
            .Setup(d => d.Send(It.IsAny<CreateTeamsGroupChatCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<TeamsCreatedChat>, CancellationToken>((command, _) => sent = (CreateTeamsGroupChatCommand)command)
            .ReturnsAsync(Result.Success(new TeamsCreatedChat("19:long", "https://teams.microsoft.com/l/chat/long/0")));
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new CreateIncidentTeamsChatCommand(incident.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        sent!.Topic.Should().HaveLength(CreateIncidentTeamsChatCommandHandler.GraphTopicMaxLength);
        sent.Topic.Should().StartWith("INC-42 ");
        incident.TeamsChat!.Title.Should().Be(sent.Topic);
    }

    [Fact]
    public async Task Handle_ShouldNotCallGraph_WhenAChatIsAlreadyStored()
    {
        // Arrange
        var incident = await OpenIncident();
        incident.LinkTeamsChat("https://teams.microsoft.com/l/chat/pasted/0", "Bridge", EventActor.System, Now);
        var savesBefore = DbContext.SaveChangesCallCount;
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new CreateIncidentTeamsChatCommand(incident.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This incident already has a Teams chat.");
        incident.TeamsChat!.Link.Should().Be("https://teams.microsoft.com/l/chat/pasted/0");
        DbContext.SaveChangesCallCount.Should().Be(savesBefore);
        _dispatcher.Verify(
            d => d.Send(It.IsAny<CreateTeamsGroupChatCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotStoreAChat_WhenGraphRejectsIt()
    {
        // Arrange
        var incident = await OpenIncident();
        SetEmail(incident.ReportedByEmployeeId, "reported@contoso.com");
        var savesBefore = DbContext.SaveChangesCallCount;
        _dispatcher
            .Setup(d => d.Send(It.IsAny<CreateTeamsGroupChatCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<TeamsCreatedChat>("Microsoft Graph could not create the Teams chat: denied."));
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new CreateIncidentTeamsChatCommand(incident.Id, []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("could not create the Teams chat");
        incident.TeamsChat.Should().BeNull();
        DbContext.SaveChangesCallCount.Should().Be(savesBefore);
    }

    [Fact]
    public async Task Handle_ShouldNotCallGraph_WhenAnExtraPersonHasNoEmail()
    {
        // Arrange
        var incident = await OpenIncident();
        var extraId = Guid.CreateVersion7();
        SeedEmployee(extraId);
        var savesBefore = DbContext.SaveChangesCallCount;
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new CreateIncidentTeamsChatCommand(incident.Id, [extraId]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A person added for this chat has no email address.");
        incident.TeamsChat.Should().BeNull();
        DbContext.SaveChangesCallCount.Should().Be(savesBefore);
        _dispatcher.Verify(
            d => d.Send(It.IsAny<CreateTeamsGroupChatCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotCallGraph_WhenAnExtraPersonDoesNotExist()
    {
        // Arrange
        var incident = await OpenIncident();
        var savesBefore = DbContext.SaveChangesCallCount;
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new CreateIncidentTeamsChatCommand(incident.Id, [Guid.CreateVersion7()]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A person added for this chat was not found.");
        DbContext.SaveChangesCallCount.Should().Be(savesBefore);
        _dispatcher.Verify(
            d => d.Send(It.IsAny<CreateTeamsGroupChatCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private CreateIncidentTeamsChatCommandHandler CreateHandler() =>
        new(
            DbContext,
            _dispatcher.Object,
            CurrentUser.Object,
            CurrentPrincipal.Object,
            DateTimeProvider.Object,
            Logger<CreateIncidentTeamsChatCommandHandler>());

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

    private void SetEmail(Guid employeeId, string email)
    {
        var employee = DbContext.Employees.Single(e => e.Id == employeeId);
        typeof(Employee).GetProperty(nameof(Employee.Email))!
            .SetValue(employee, new EmailAddress(email));
    }

    private static void SetKey(Incident incident, int key) =>
        typeof(Incident).GetField("<Key>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(incident, key);

    private static void SetTitle(Incident incident, string title) =>
        typeof(Incident).GetProperty(nameof(Incident.Title))!.SetValue(incident, title);

    private static void SetCommander(Incident incident, Guid commanderId) =>
        typeof(Incident).GetProperty(nameof(Incident.CommanderEmployeeId))!.SetValue(incident, commanderId);
}
