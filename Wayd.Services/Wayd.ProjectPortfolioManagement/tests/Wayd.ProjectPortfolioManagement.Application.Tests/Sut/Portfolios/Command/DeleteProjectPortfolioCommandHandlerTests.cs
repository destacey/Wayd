using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Command;

public sealed class DeleteProjectPortfolioCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 17, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ILinkWriter> _linkWriter = new();

    public DeleteProjectPortfolioCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    private DeleteProjectPortfolioCommandHandler CreateHandler() => new(
        _dbContext, _currentUser.Object, Mock.Of<ILogger<DeleteProjectPortfolioCommandHandler>>(), _dateTimeProvider, _linkWriter.Object);

    [Fact]
    public async Task Handle_ShouldRemoveThePortfoliosLinksInTheSameSave()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().AsProposed();
        _dbContext.AddPortfolio(portfolio);
        var removedBeforeSave = false;
        _linkWriter
            .Setup(w => w.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .Callback(() => removedBeforeSave = _dbContext.SaveChangesCallCount == 0)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(new DeleteProjectPortfolioCommand(portfolio.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.Portfolios.Should().BeEmpty();
        _linkWriter.Verify(w => w.RemoveAll("ppm.portfolio", portfolio.Id, It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Once);
        removedBeforeSave.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheLinks_WhenThePortfolioCannotBeDeleted()
    {
        // Arrange — only a proposed portfolio can be deleted
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        _dbContext.AddPortfolio(portfolio);

        // Act
        var result = await CreateHandler().Handle(new DeleteProjectPortfolioCommand(portfolio.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.Portfolios.Should().ContainSingle();
        _linkWriter.Verify(w => w.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Never);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
