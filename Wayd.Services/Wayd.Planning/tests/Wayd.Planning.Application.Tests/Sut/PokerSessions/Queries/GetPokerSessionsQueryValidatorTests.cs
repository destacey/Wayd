using FluentValidation.TestHelper;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PokerSessions.Queries;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Queries;

public sealed class GetPokerSessionsQueryValidatorTests
{
    private readonly GetPokerSessionsQueryValidator _validator = new();

    [Fact]
    public void NoHost_Passes()
    {
        // Act
        var result = _validator.TestValidate(new GetPokerSessionsQuery());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void KindAndId_Passes()
    {
        // Act
        var result = _validator.TestValidate(new GetPokerSessionsQuery(HostKind: PokerSessionHostKind.IntakeDesk, HostId: Guid.NewGuid()));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void KindWithoutId_Fails()
    {
        // Act
        var result = _validator.TestValidate(new GetPokerSessionsQuery(HostKind: PokerSessionHostKind.IntakeDesk));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IdWithoutKind_Fails()
    {
        // Act
        var result = _validator.TestValidate(new GetPokerSessionsQuery(HostId: Guid.NewGuid()));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UndefinedKind_Fails()
    {
        // Act
        var result = _validator.TestValidate(new GetPokerSessionsQuery(HostKind: (PokerSessionHostKind)99, HostId: Guid.NewGuid()));

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.HostKind);
    }
}
