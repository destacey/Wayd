using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands;
using Wayd.AppIntegration.Application.Connections.ExternalSystems;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.AzureDevOps;

public sealed class CreateAzureDevOpsConnectionCommandHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly CreateAzureDevOpsConnectionCommandHandler _sut;

    public CreateAzureDevOpsConnectionCommandHandlerTests()
    {
        var urls = new Mock<IAzureDevOpsUrlResolver>();
        urls.Setup(r => r.GetOrganizationUrl(It.IsAny<string>())).Returns((string org) => $"https://dev.azure.com/{org}");

        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.Use<IExternalSystemLinker>(new ExternalSystemLinker(_db, urls.Object));
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());

        _sut = _mocker.CreateInstance<CreateAzureDevOpsConnectionCommandHandler>();
    }

    [Fact]
    public async Task Handle_TiesTheNewConnectionToTheOrganizationItReaches()
    {
        // Arrange
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetSystemId(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("org-id"));

        // Act
        var result = await _sut.Handle(new CreateAzureDevOpsConnectionCommand("Boards", null, "contoso", "pat"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var system = _db.ExternalSystems.Should().ContainSingle().Subject;
        system.Connector.Should().Be(Connector.AzureDevOps);
        system.SystemId.Should().Be("org-id");
        system.Name.Should().Be("contoso");
        system.BaseUrl.Should().Be("https://dev.azure.com/contoso");
        _db.AzureDevOpsBoardsConnections.Single(c => c.Id == result.Value).ExternalSystemId.Should().Be(system.Id);
        _db.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LeavesTheConnectionUnlinked_WhenTheOrganizationCannotBeReached()
    {
        // Arrange
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetSystemId(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>("unreachable"));

        // Act
        var result = await _sut.Handle(new CreateAzureDevOpsConnectionCommand("Boards", null, "contoso", "pat"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _db.ExternalSystems.Should().BeEmpty();
        _db.AzureDevOpsBoardsConnections.Single(c => c.Id == result.Value).ExternalSystemId.Should().BeNull();
    }
}
