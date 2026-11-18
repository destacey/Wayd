using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis;

public sealed class KpiCommandScopeTests
{
    private readonly KpiTestHarness _harness = new();

    [Fact]
    public async Task KpiForManaging_ShouldLoadTheKpi_WhenTheCallerMayManageItsRecord()
    {
        // Arrange
        var kpi = _harness.AddKpi();

        // Act
        var result = await _harness.Scope().KpiForManaging(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, withMeasurements: true, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeSameAs(kpi);
    }

    [Fact]
    public async Task KpiForManaging_ShouldBeForbidden_WhenTheCallerMayNotManage()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        _harness.Subject(canManage: false);
        _harness.Administer(false);

        // Act
        var result = await _harness.Scope().KpiForManaging(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, withMeasurements: false, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CannotManage);
    }

    [Fact]
    public async Task KpiForManaging_ShouldBeNotFound_WhenTheRecordIsHidden()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        _harness.SubjectHidden();

        // Act
        var result = await _harness.Scope().KpiForManaging(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, withMeasurements: false, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.NotFound);
    }

    [Fact]
    public async Task KpiForManaging_ShouldBeNotFound_ForAKpiOnAnotherRecord()
    {
        // Arrange
        var elsewhere = _harness.AddKpi(subjectId: Guid.NewGuid());

        // Act
        var result = await _harness.Scope().KpiForManaging(KpiTestHarness.Gadget.Key, _harness.SubjectId, elsewhere.Id, withMeasurements: false, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.NotFound);
    }

    [Fact]
    public async Task CheckCategoryAssignable_ShouldRefuseAnInactiveCategory_ButKeepTheOneAlreadyAssigned()
    {
        // Arrange
        var category = KpiCategory.Create("Flow", null, 0, EventActor.System, _harness.DateTimeProvider.Now).Value;
        category.Deactivate(EventActor.System, _harness.DateTimeProvider.Now);
        _harness.DbContext.AddCategories(category);
        var scope = _harness.Scope();

        // Act
        var assigning = await scope.CheckCategoryAssignable(category.Id, null, TestContext.Current.CancellationToken);
        var keeping = await scope.CheckCategoryAssignable(category.Id, category.Id, TestContext.Current.CancellationToken);
        var unknown = await scope.CheckCategoryAssignable(Guid.NewGuid(), null, TestContext.Current.CancellationToken);

        // Assert
        assigning.Error.Should().Be(KpiAccessErrors.CategoryInactive);
        keeping.IsSuccess.Should().BeTrue();
        unknown.Error.Should().Be(KpiAccessErrors.CategoryNotFound);
    }

    [Fact]
    public async Task Save_ShouldReportACollisionAsAConflict()
    {
        // Arrange
        _harness.DbContext.NextSaveThrows = new DbUpdateConcurrencyException("collided");

        // Act
        var result = await _harness.Scope().Save(TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.Conflict);
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    [InlineData(547)]
    public async Task Save_ShouldReportAUniqueKeyOrForeignKeyCollisionAsAConflict(int number)
    {
        // Arrange
        _harness.DbContext.NextSaveThrows = new DbUpdateException("collided", new FakeProviderException(number));

        // Act
        var result = await _harness.Scope().Save(TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.Conflict);
    }

    [Fact]
    public async Task Save_ShouldThrowAnyOtherFailedSave()
    {
        // Arrange
        _harness.DbContext.NextSaveThrows = new DbUpdateException("truncated", new FakeProviderException(2628));

        // Act
        var save = () => _harness.Scope().Save(TestContext.Current.CancellationToken);

        // Assert
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>Stands in for SqlException, which the scope reads only through its <c>Number</c>.</summary>
    private sealed class FakeProviderException(int number) : Exception("provider")
    {
        public int Number { get; } = number;
    }
}
