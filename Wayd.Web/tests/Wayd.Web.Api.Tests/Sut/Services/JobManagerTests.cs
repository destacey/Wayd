using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Planning.Application.PlanningTeams.Commands;
using Wayd.ProductManagement.Application.ProductTeams.Commands;
using Wayd.ProjectPortfolioManagement.Application.PpmTeams.Commands;
using Wayd.Web.Api.Services;
using Wayd.Work.Application.WorkTeams.Commands;

namespace Wayd.Web.Api.Tests.Sut.Services;

public sealed class JobManagerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IDispatcher> _dispatcher;

    public JobManagerTests()
    {
        _dispatcher = _mocker.GetMock<IDispatcher>();
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(Instant.FromUtc(2026, 6, 1, 12, 0));
        _dispatcher.Setup(d => d.Send(It.IsAny<GetSimpleTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ISimpleTeam>());
        _dispatcher.Setup(d => d.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task RunSyncTeams_SyncsTheOtherModules_WhenOneThrows()
    {
        // Arrange
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncPlanningTeamsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("planning failed"));
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncTeams(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncPpmTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncProductTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncWorkTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncTeams_StopsWhenCancelled()
    {
        // Arrange
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncPlanningTeamsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncTeams(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncPpmTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
