using FluentAssertions;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkStatusCategories.Queries;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkStatusCategories.Queries;

public sealed class GetWorkStatusCategoriesQueryTests
{
    [Fact]
    public async Task Handle_ShouldListEveryCategory_DescribedForWork()
    {
        // Arrange
        var sut = new GetWorkStatusCategoriesQueryHandler();

        // Act
        var categories = await sut.Handle(new GetWorkStatusCategoriesQuery(), TestContext.Current.CancellationToken);

        // Assert
        categories.Select(c => (c.Id, c.Name, c.Description, c.Order)).Should().Equal(
            ((int)StatusCategory.Proposed, "Proposed", "The work has been proposed but not yet started.", 1),
            ((int)StatusCategory.Active, "Active", "The work is currently being performed.", 2),
            ((int)StatusCategory.Done, "Done", "The work has been completed.", 3),
            ((int)StatusCategory.Removed, "Removed", "The work has been removed from the backlog without being completed.", 4));
    }
}
