using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Commands;

public sealed class DeleteProgramCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 17, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ILinkWriter> _linkWriter = new();

    public DeleteProgramCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    private DeleteProgramCommandHandler CreateHandler() => new(
        _dbContext, _currentUser.Object, Mock.Of<ILogger<DeleteProgramCommandHandler>>(), _dateTimeProvider, _linkWriter.Object);

    /// <summary>
    /// A program in an active portfolio. The portfolio's programs are populated directly because the fake context
    /// ignores Include.
    /// </summary>
    private Program SeedProgram(bool proposed)
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var program = proposed
            ? new ProgramFaker().AsProposed(portfolio.Id)
            : new ProgramFaker().AsActive(_dateTimeProvider, portfolio.Id);
        GenericExtensions.GetPrivateHashSet<Program>(portfolio, "_programs").Add(program);
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddProgram(program);
        return program;
    }

    [Fact]
    public async Task Handle_ShouldRemoveTheProgramsLinksInTheSameSave()
    {
        // Arrange
        var program = SeedProgram(proposed: true);
        var removedBeforeSave = false;
        _linkWriter
            .Setup(w => w.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .Callback(() => removedBeforeSave = _dbContext.SaveChangesCallCount == 0)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(new DeleteProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _linkWriter.Verify(w => w.RemoveAll("ppm.program", program.Id, It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Once);
        removedBeforeSave.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheLinks_WhenTheProgramCannotBeDeleted()
    {
        // Arrange — only a proposed program can be deleted
        var program = SeedProgram(proposed: false);

        // Act
        var result = await CreateHandler().Handle(new DeleteProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _linkWriter.Verify(w => w.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Never);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
