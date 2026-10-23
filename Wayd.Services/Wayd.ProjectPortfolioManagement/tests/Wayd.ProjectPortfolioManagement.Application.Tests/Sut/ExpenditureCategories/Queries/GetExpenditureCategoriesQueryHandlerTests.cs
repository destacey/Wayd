using FluentAssertions;
using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ExpenditureCategories.Queries;

public sealed class GetExpenditureCategoriesQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly GetExpenditureCategoriesQueryHandler _handler;

    public GetExpenditureCategoriesQueryHandlerTests()
    {
        _handler = new GetExpenditureCategoriesQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_OrdersByNameThenId()
    {
        // Arrange — two categories share a name, so only the id separates them
        _dbContext.AddExpenditureCategories([
            new ExpenditureCategoryFaker().WithId(4).WithName("Opex").Generate(),
            new ExpenditureCategoryFaker().WithId(9).WithName("Capex").Generate(),
            new ExpenditureCategoryFaker().WithId(2).WithName("Opex").Generate(),
            new ExpenditureCategoryFaker().WithId(7).WithName("Licensing").Generate(),
        ]);

        // Act
        var result = await _handler.Handle(new GetExpenditureCategoriesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(c => c.Id).Should().Equal(9, 7, 2, 4);
    }
}
