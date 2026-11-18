using FluentAssertions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis;

public sealed class KpiSubjectAccessResolverTests
{
    private static readonly KpiSubjectTypeDescriptor Gadget = new("test.kpi-access.gadget", "Gadget", "Gadget");

    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<IKpiSubjectType> _subjectType = new();

    public KpiSubjectAccessResolverTests()
    {
        KpiSubjectTypes.Register(Gadget);
        _subjectType.Setup(t => t.SubjectType).Returns(Gadget.Key);
    }

    private KpiSubjectAccessResolver CreateSut() => new([_subjectType.Object], _currentPrincipal.Object);

    private void SubjectAnswers(KpiSubjectAccess? access) =>
        _subjectType.Setup(t => t.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(access);

    private void HoldsAdminister(bool holds) =>
        _currentPrincipal.Setup(p => p.HasPermission(KpiSubjectAccessResolver.AdministerPermission, It.IsAny<CancellationToken>())).ReturnsAsync(holds);

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    public async Task Resolve_ShouldWidenByAdministerAndNarrowByAReadOnlyToken(
        bool subjectAllows, bool holdsAdminister, bool readOnly, bool expected)
    {
        // Arrange
        SubjectAnswers(new KpiSubjectAccess("Gadget One", "G1", subjectAllows));
        HoldsAdminister(holdsAdminister);
        _currentPrincipal.Setup(p => p.IsReadOnly).Returns(readOnly);
        var subjectId = Guid.NewGuid();

        // Act
        var result = await CreateSut().Resolve(" TEST.kpi-access.gadget ", subjectId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var resolved = result.Value!;
        resolved.SubjectType.Should().Be(Gadget.Key);
        resolved.SubjectId.Should().Be(subjectId);
        resolved.Access.Name.Should().Be("Gadget One");
        resolved.Access.CanManage.Should().Be(expected);
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheSubjectTypeCannotFindOrShowTheRecord()
    {
        // Arrange
        SubjectAnswers(null);
        HoldsAdminister(true);

        // Act
        var result = await CreateSut().Resolve(Gadget.Key, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldFail_ForARegisteredTypeNoModuleAnswersFor()
    {
        // Arrange
        var orphan = new KpiSubjectTypeDescriptor("test.kpi-access.orphan", "Orphan", "Orphan");
        KpiSubjectTypes.Register(orphan);

        // Act
        var result = await CreateSut().Resolve(orphan.Key, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiErrors.UnknownSubjectType(orphan.Key));
        _subjectType.Verify(t => t.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_ShouldFail_ForAnUnregisteredType()
    {
        // Arrange — nothing registered under the key

        // Act
        var result = await CreateSut().Resolve("test.kpi-access.nothing", Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiErrors.UnknownSubjectType("test.kpi-access.nothing"));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public async Task CanManageCategories_ShouldNeedAdministerAndAWriteToken(bool holdsAdminister, bool readOnly, bool expected)
    {
        // Arrange
        HoldsAdminister(holdsAdminister);
        _currentPrincipal.Setup(p => p.IsReadOnly).Returns(readOnly);

        // Act
        var canManage = await CreateSut().CanManageCategories(TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().Be(expected);
    }
}
