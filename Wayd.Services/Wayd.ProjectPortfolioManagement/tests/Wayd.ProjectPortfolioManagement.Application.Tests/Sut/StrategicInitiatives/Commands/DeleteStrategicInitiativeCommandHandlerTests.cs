using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives.Commands;

public sealed class DeleteStrategicInitiativeCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 17, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ILinkWriter> _linkWriter = new();

    public DeleteStrategicInitiativeCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    private DeleteStrategicInitiativeCommandHandler CreateHandler() => new(
        _dbContext, _currentUser.Object, Mock.Of<ILogger<DeleteStrategicInitiativeCommandHandler>>(), _dateTimeProvider, _linkWriter.Object);

    /// <summary>An initiative of the given status in an active portfolio that holds it.</summary>
    private StrategicInitiative SeedInitiative(StrategicInitiativeStatus status)
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider).AddStrategicThemes(5, _dateTimeProvider);
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddStrategicInitiatives(portfolio.StrategicInitiatives);
        return portfolio.StrategicInitiatives.First(i => i.Status == status);
    }

    [Fact]
    public async Task Handle_ShouldRemoveTheInitiativesLinksInTheSameSave()
    {
        // Arrange
        var initiative = SeedInitiative(StrategicInitiativeStatus.Proposed);
        var removedBeforeSave = false;
        _linkWriter
            .Setup(w => w.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .Callback(() => removedBeforeSave = _dbContext.SaveChangesCallCount == 0)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(new DeleteStrategicInitiativeCommand(initiative.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _linkWriter.Verify(w => w.RemoveAll("ppm.strategic-initiative", initiative.Id, It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Once);
        removedBeforeSave.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheLinks_WhenTheInitiativeDoesNotExist()
    {
        // Arrange
        SeedInitiative(StrategicInitiativeStatus.Proposed);

        // Act
        var result = await CreateHandler().Handle(new DeleteStrategicInitiativeCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _linkWriter.Verify(w => w.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Never);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
