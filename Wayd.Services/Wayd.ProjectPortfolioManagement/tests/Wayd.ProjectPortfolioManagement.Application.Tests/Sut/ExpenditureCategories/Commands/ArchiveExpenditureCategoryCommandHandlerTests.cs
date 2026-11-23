using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ExpenditureCategories.Commands;

public sealed class ArchiveExpenditureCategoryCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();

    private ArchiveExpenditureCategoryCommandHandler CreateHandler() =>
        new(_dbContext, Mock.Of<ILogger<ArchiveExpenditureCategoryCommandHandler>>());

    [Fact]
    public async Task Handle_ShouldArchiveAnActiveCategory()
    {
        // Arrange
        var category = new ExpenditureCategoryFaker().WithState(ExpenditureCategoryState.Active).Generate();
        _dbContext.AddExpenditureCategory(category);

        // Act
        var result = await CreateHandler().Handle(new ArchiveExpenditureCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        category.State.Should().Be(ExpenditureCategoryState.Archived);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(ExpenditureCategoryState.Proposed)]
    [InlineData(ExpenditureCategoryState.Archived)]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheCategoryIsNotActive(ExpenditureCategoryState state)
    {
        // Arrange
        var category = new ExpenditureCategoryFaker().WithState(state).Generate();
        _dbContext.AddExpenditureCategory(category);

        // Act
        var result = await CreateHandler().Handle(new ArchiveExpenditureCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active categories can be archived.");
        category.State.Should().Be(state);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheCategoryDoesNotExist()
    {
        // Arrange
        var category = new ExpenditureCategoryFaker().WithId(1).WithState(ExpenditureCategoryState.Active).Generate();
        _dbContext.AddExpenditureCategory(category);

        // Act
        var result = await CreateHandler().Handle(new ArchiveExpenditureCategoryCommand(2), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expenditure Category not found.");
        category.State.Should().Be(ExpenditureCategoryState.Active);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
