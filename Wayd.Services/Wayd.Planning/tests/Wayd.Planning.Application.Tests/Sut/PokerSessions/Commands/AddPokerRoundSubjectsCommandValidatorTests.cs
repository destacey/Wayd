using FluentValidation.TestHelper;
using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public sealed class AddPokerRoundSubjectsCommandValidatorTests
{
    private readonly AddPokerRoundSubjectsCommandValidator _validator = new();

    private static PokerRoundSubjectInput ARequest() =>
        new(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), "42", "Export invoices", null);

    private static AddPokerRoundSubjectsCommand Command(params PokerRoundSubjectInput[] subjects) =>
        new(Guid.NewGuid(), PokerSessionHostKind.IntakeDesk, Guid.NewGuid(), subjects);

    [Fact]
    public void ARecord_Passes()
    {
        // Act
        var result = _validator.TestValidate(Command(ARequest()));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NoRecords_Fails()
    {
        // Act
        var result = _validator.TestValidate(Command());

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Subjects);
    }

    [Fact]
    public void TooManyRecords_Fails()
    {
        // Arrange
        var subjects = Enumerable.Range(0, PokerRoundSubjectInputValidator.MaxSubjectsPerRequest + 1).Select(_ => ARequest()).ToArray();

        // Act
        var result = _validator.TestValidate(Command(subjects));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Subjects);
    }

    [Fact]
    public void EmptyHost_Fails()
    {
        // Act
        var result = _validator.TestValidate(Command(ARequest()) with { HostId = Guid.Empty });

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.HostId);
    }
}
