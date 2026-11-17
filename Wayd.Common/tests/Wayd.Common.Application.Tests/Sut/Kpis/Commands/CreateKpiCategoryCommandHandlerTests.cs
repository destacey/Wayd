using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Commands;

public sealed class CreateKpiCategoryCommandHandlerTests
{
    private readonly KpiTestHarness _harness = new();

    public CreateKpiCategoryCommandHandlerTests()
    {
        _harness.Administer(true);
    }

    private CreateKpiCategoryCommandHandler CreateHandler() => new(_harness.Scope());

    [Fact]
    public async Task Handle_ShouldAddTheCategoryLast()
    {
        // Arrange
        _harness.DbContext.AddCategories(
            KpiCategory.Create("Flow", null, 0, EventActor.System, _harness.DateTimeProvider.Now).Value,
            KpiCategory.Create("Quality", null, 3, EventActor.System, _harness.DateTimeProvider.Now).Value);

        // Act
        var result = await CreateHandler().Handle(new CreateKpiCategoryCommand(" People ", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var created = _harness.DbContext.KpiCategories.Single(c => c.Id == result.Value);
        created.Name.Should().Be("People");
        created.Order.Should().Be(4);
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseANameAnotherCategoryHas()
    {
        // Arrange
        _harness.DbContext.AddCategories(KpiCategory.Create("Flow", null, 0, EventActor.System, _harness.DateTimeProvider.Now).Value);

        // Act
        var result = await CreateHandler().Handle(new CreateKpiCategoryCommand("Flow ", null), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.DuplicateCategoryName);
        _harness.DbContext.KpiCategories.Should().ContainSingle();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_ForAReadOnlyToken()
    {
        // Arrange
        _harness.CurrentPrincipal.Setup(p => p.IsReadOnly).Returns(true);

        // Act
        var result = await CreateHandler().Handle(new CreateKpiCategoryCommand("People", null), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CannotManageCategories);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}