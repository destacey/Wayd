using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Moq;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public class CreateProjectCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly CreateProjectCommandHandler _handler;
    private readonly Mock<ILogger<CreateProjectCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly string _actorUserId = Guid.NewGuid().ToString();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    public CreateProjectCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<CreateProjectCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_actorUserId);
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns(_actorEmployeeId);

        _handler = new CreateProjectCommandHandler(_dbContext, _mockLogger.Object, _dateTimeProvider, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenExpenditureCategoryNotFound()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        _dbContext.AddPortfolio(portfolio);

        var command = new CreateProjectCommand(
            "Test Project", "Description", null, null, new ProjectKey("TEST"), 999,
            null, portfolio.Id, null, null, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expenditure Category not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenExpenditureCategoryNotActive()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateProposed();
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddExpenditureCategory(expenditureCategory);

        var command = new CreateProjectCommand(
            "Test Project", "Description", null, null, new ProjectKey("TEST"), expenditureCategory.Id,
            null, portfolio.Id, null, null, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expenditure Category is not active.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPortfolioNotFound()
    {
        // Arrange
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateActive();
        _dbContext.AddExpenditureCategory(expenditureCategory);

        var command = new CreateProjectCommand(
            "Test Project", "Description", null, null, new ProjectKey("TEST"), expenditureCategory.Id,
            null, Guid.NewGuid(), null, null, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Portfolio not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldCreateProject_WithoutLifecycle()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateActive();
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddExpenditureCategory(expenditureCategory);

        var command = new CreateProjectCommand(
            "Test Project", "Description", null, null, new ProjectKey("TEST"), expenditureCategory.Id,
            null, portfolio.Id, null, null, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Key.Should().NotBeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCreateProject_WithLifecycle()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateActive();
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithStages(
            ("Plan", "Planning stage"),
            ("Execute", "Execution stage"),
            ("Deliver", "Delivery stage"));
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddExpenditureCategory(expenditureCategory);
        _dbContext.AddProjectLifecycle(lifecycle);

        var command = new CreateProjectCommand(
            "Test Project", "Description", null, null, new ProjectKey("TEST"), expenditureCategory.Id,
            null, portfolio.Id, null, lifecycle.Id, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Key.Should().NotBeNull();
        _dbContext.SaveChangesCallCount.Should().Be(2); // one for project, one for lifecycle assignment

        var project = portfolio.Projects.First();
        project.ProjectLifecycleId.Should().Be(lifecycle.Id);
        project.Stages.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLifecycleNotFound()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateActive();
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddExpenditureCategory(expenditureCategory);

        var command = new CreateProjectCommand(
            "Test Project", "Description", null, null, new ProjectKey("TEST"), expenditureCategory.Id,
            null, portfolio.Id, null, Guid.NewGuid(), null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Lifecycle not found.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLifecycleNotActive()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateActive();
        var lifecycle = new ProjectLifecycleFaker().AsProposedWithStages(
            ("Plan", "Planning stage"));
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddExpenditureCategory(expenditureCategory);
        _dbContext.AddProjectLifecycle(lifecycle);

        var command = new CreateProjectCommand(
            "Test Project", "Description", null, null, new ProjectKey("TEST"), expenditureCategory.Id,
            null, portfolio.Id, null, lifecycle.Id, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Only active lifecycles");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
