using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public sealed class DeleteProjectCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 10, 30, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDiscussionWriter> _discussionWriter = new();
    private readonly Mock<ILinkWriter> _linkWriter = new();
    private readonly Mock<IMarkdownAttachments> _markdownAttachments = new();

    public DeleteProjectCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    private DeleteProjectCommandHandler CreateHandler() => new(
        _dbContext, _currentUser.Object, Mock.Of<ILogger<DeleteProjectCommandHandler>>(), _dateTimeProvider, _discussionWriter.Object, _linkWriter.Object, _markdownAttachments.Object);

    private Project SeedProposedProject()
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider).AddProjects(3, _dateTimeProvider);
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddProjects(portfolio.Projects);
        return portfolio.Projects.First(p => p.Status == ProjectStatus.Proposed);
    }

    [Fact]
    public async Task Handle_ShouldDeleteTheProjectsConversationInTheSameSave()
    {
        // Arrange
        var project = SeedProposedProject();
        var deletedBeforeSave = false;
        _discussionWriter
            .Setup(w => w.DeleteChannel(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .Callback(() => deletedBeforeSave = _dbContext.SaveChangesCallCount == 0)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(new DeleteProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _discussionWriter.Verify(w => w.DeleteChannel("ppm.project", project.Id, It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Once);
        deletedBeforeSave.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldDeleteTheProjectsImagesInTheSameSave()
    {
        // Arrange
        var project = SeedProposedProject();
        var removedBeforeSave = false;
        _markdownAttachments
            .Setup(a => a.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => removedBeforeSave = _dbContext.SaveChangesCallCount == 0)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(new DeleteProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _markdownAttachments.Verify(a => a.RemoveAll("ppm.project", project.Id, It.IsAny<CancellationToken>()), Times.Once);
        removedBeforeSave.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheConversation_WhenTheProjectCannotBeDeleted()
    {
        // Arrange
        SeedProposedProject();

        // Act
        var result = await CreateHandler().Handle(new DeleteProjectCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _discussionWriter.Verify(w => w.DeleteChannel(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Never);
        _linkWriter.Verify(w => w.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Never);
        _markdownAttachments.Verify(a => a.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRemoveTheProjectsLinksInTheSameSave()
    {
        // Arrange
        var project = SeedProposedProject();
        var removedBeforeSave = false;
        _linkWriter
            .Setup(w => w.RemoveAll(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .Callback(() => removedBeforeSave = _dbContext.SaveChangesCallCount == 0)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(new DeleteProjectCommand(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _linkWriter.Verify(w => w.RemoveAll("ppm.project", project.Id, It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Once);
        removedBeforeSave.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    public void Dispose() => _dbContext.Dispose();
}
