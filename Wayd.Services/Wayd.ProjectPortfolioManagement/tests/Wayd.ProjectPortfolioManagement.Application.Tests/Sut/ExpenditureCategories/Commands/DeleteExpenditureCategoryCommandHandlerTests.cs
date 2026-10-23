using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ExpenditureCategories.Commands;

/// <summary>Which expenditure categories can be deleted.</summary>
public sealed class DeleteExpenditureCategoryCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly DeleteExpenditureCategoryCommandHandler _handler;

    public DeleteExpenditureCategoryCommandHandlerTests()
    {
        _handler = new DeleteExpenditureCategoryCommandHandler(
            _dbContext, new Mock<ILogger<DeleteExpenditureCategoryCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldDelete_WhenTheCategoryIsProposedAndUnused()
    {
        // Arrange
        var category = new ExpenditureCategoryFaker().GenerateProposed();
        _dbContext.AddExpenditureCategory(category);

        // Act
        var result = await _handler.Handle(new DeleteExpenditureCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheCategoryIsActive()
    {
        // Arrange
        var category = new ExpenditureCategoryFaker().GenerateActive();
        _dbContext.AddExpenditureCategory(category);

        // Act
        var result = await _handler.Handle(new DeleteExpenditureCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expenditure Category cannot be deleted.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAProjectUsesTheProposedCategory()
    {
        // Arrange — deleting it would otherwise fail on the project's foreign key.
        var category = new ExpenditureCategoryFaker().GenerateProposed();
        _dbContext.AddExpenditureCategory(category);
        _dbContext.AddProject(new ProjectFaker().WithExpenditureCategoryId(category.Id).Generate());

        // Act
        var result = await _handler.Handle(new DeleteExpenditureCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expenditure Category is used by a project and cannot be deleted.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
