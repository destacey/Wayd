using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Enums;

public sealed class NeedByBasisExtensionsTests
{
    [Theory]
    [InlineData(NeedByBasis.Regulatory, NeedByBasisFamily.FixedForUs)]
    [InlineData(NeedByBasis.Contractual, NeedByBasisFamily.FixedForUs)]
    [InlineData(NeedByBasis.ExternalEvent, NeedByBasisFamily.FixedForUs)]
    [InlineData(NeedByBasis.Commitment, NeedByBasisFamily.ChosenByUs)]
    [InlineData(NeedByBasis.RequesterPreference, NeedByBasisFamily.ChosenByUs)]
    public void Family_PlacesEachBasisInItsFamily(NeedByBasis basis, NeedByBasisFamily expected)
    {
        // Act
        var result = basis.Family();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Family_CoversEveryBasis()
    {
        // Arrange — a basis added without a family would throw at the first request that used it
        var bases = Enum.GetValues<NeedByBasis>();

        // Act
        var act = () => bases.Select(b => b.Family()).ToList();

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(NeedByBasis.Commitment, true)]
    [InlineData(NeedByBasis.Regulatory, false)]
    [InlineData(NeedByBasis.RequesterPreference, false)]
    public void RecordsCommitment_IsTrueOnlyForACommitment(NeedByBasis basis, bool expected)
    {
        // Act
        var result = basis.RecordsCommitment();

        // Assert
        result.Should().Be(expected);
    }
}
