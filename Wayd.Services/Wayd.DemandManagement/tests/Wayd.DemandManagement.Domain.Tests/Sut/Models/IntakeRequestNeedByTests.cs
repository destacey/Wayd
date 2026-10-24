using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeRequestNeedByTests
{
    private static readonly LocalDate Date = new(2026, 12, 31);

    [Fact]
    public void Create_ForAFixedBasisWithAReference_Succeeds()
    {
        // Act
        var result = IntakeRequestNeedBy.Create(Date, NeedByBasis.Regulatory, " PCI DSS 4.0, req. 6.4.3 ", null, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Date.Should().Be(Date);
        result.Value.Basis.Should().Be(NeedByBasis.Regulatory);
        result.Value.Reference.Should().Be("PCI DSS 4.0, req. 6.4.3");
        result.Value.CommittedById.Should().BeNull();
        result.Value.CommittedOn.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Create_WithoutAReference_Fails(string? reference)
    {
        // Act
        var result = IntakeRequestNeedBy.Create(Date, NeedByBasis.RequesterPreference, reference, null, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A need-by date needs a reference saying what the date rests on.");
    }

    [Fact]
    public void Create_ForACommitmentWithWhoAndWhen_Succeeds()
    {
        // Arrange
        var committedById = Guid.NewGuid();
        var committedOn = new LocalDate(2026, 9, 3);

        // Act
        var result = IntakeRequestNeedBy.Create(Date, NeedByBasis.Commitment, "Steering committee", committedById, committedOn);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CommittedById.Should().Be(committedById);
        result.Value.CommittedOn.Should().Be(committedOn);
    }

    [Fact]
    public void Create_ForACommitmentMissingWhoMadeIt_Fails()
    {
        // Act
        var result = IntakeRequestNeedBy.Create(Date, NeedByBasis.Commitment, "Steering committee", null, new LocalDate(2026, 9, 3));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A commitment needs who made it and the date it was made.");
    }

    [Fact]
    public void Create_ForACommitmentMissingWhenItWasMade_Fails()
    {
        // Act
        var result = IntakeRequestNeedBy.Create(Date, NeedByBasis.Commitment, "Steering committee", Guid.NewGuid(), null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_ForAnotherBasisCarryingACommitment_Fails()
    {
        // Act
        var result = IntakeRequestNeedBy.Create(Date, NeedByBasis.Contractual, "MSA clause 12", Guid.NewGuid(), new LocalDate(2026, 9, 3));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a commitment records who made it and when.");
    }

    [Fact]
    public void Create_WithAnUndefinedBasis_Fails()
    {
        // Act
        var result = IntakeRequestNeedBy.Create(Date, (NeedByBasis)99, "A note", null, null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Equality_ComparesEveryPart()
    {
        // Arrange
        var a = IntakeRequestNeedBy.Create(Date, NeedByBasis.Regulatory, "Rule 1", null, null).Value;
        var b = IntakeRequestNeedBy.Create(Date, NeedByBasis.Regulatory, "Rule 1", null, null).Value;
        var c = IntakeRequestNeedBy.Create(Date, NeedByBasis.Regulatory, "Rule 2", null, null).Value;

        // Act & Assert
        a.Should().Be(b);
        a.Should().NotBe(c);
    }
}
