using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Commands;

public sealed class DeleteKpiCategoryCommandHandlerTests
{
    private readonly KpiTestHarness _harness = new();

    public DeleteKpiCategoryCommandHandlerTests()
    {
        _harness.Administer(true);
    }

    private DeleteKpiCategoryCommandHandler CreateHandler() => new(_harness.Scope());

    private KpiCategory AddCategory()
    {
        var category = KpiCategory.Create("Flow", null, 0, EventActor.System, _harness.DateTimeProvider.Now).Value;
        _harness.DbContext.AddCategories(category);
        return category;
    }

    [Fact]
    public async Task Handle_ShouldDeleteACategoryNoKpiUses()
    {
        // Arrange
        var category = AddCategory();

        // Act
        var result = await CreateHandler().Handle(new DeleteKpiCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _harness.DbContext.KpiCategories.Should().BeEmpty();
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenAClosedKpiStillHasIt()
    {
        // Arrange
        var category = AddCategory();
        var kpi = _harness.AddKpi(categoryId: category.Id);
        kpi.Close(EventActor.System, _harness.DateTimeProvider.Now);

        // Act
        var result = await CreateHandler().Handle(new DeleteKpiCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CategoryInUse);
        _harness.DbContext.KpiCategories.Should().ContainSingle();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WithoutTheAdministerPermission()
    {
        // Arrange
        var category = AddCategory();
        _harness.Administer(false);

        // Act
        var result = await CreateHandler().Handle(new DeleteKpiCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CannotManageCategories);
        _harness.DbContext.KpiCategories.Should().ContainSingle();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}