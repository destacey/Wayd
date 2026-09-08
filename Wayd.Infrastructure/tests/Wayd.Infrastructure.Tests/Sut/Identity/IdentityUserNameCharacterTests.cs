using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayd.Common.Extensions;
using Wayd.Common.Models;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Tests.Sut.Identity;

/// <summary>
/// A username is an address or an address-shaped identifier — the email for a locally created account, the
/// UPN for an Entra one — so the allowed-character set has to admit everything an address may contain.
/// </summary>
/// <remarks>
/// The two rules are applied a step apart and by different components: the create-user validator checks
/// the value is a valid email, then Identity checks the same string as a username. A character the first
/// accepts and the second rejects is not a validation failure anyone can act on — the account simply
/// cannot be created, and the message talks about usernames when what was supplied was an address.
/// </remarks>
public sealed class IdentityUserNameCharacterTests
{
    private static string AllowedCharacters()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentity();

        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value.User.AllowedUserNameCharacters;
    }

    [Theory]
    // The characters RFC 5322 permits unquoted in a local part, which is the half a surname lands in.
    [InlineData("o'brien@example.com", "an apostrophe, as in O'Brien")]
    [InlineData("d'angelo@example.com", "an apostrophe, as in D'Angelo")]
    [InlineData("first.last@example.com", "a dot")]
    [InlineData("first-last@example.com", "a hyphen")]
    [InlineData("first_last@example.com", "an underscore")]
    [InlineData("first+tag@example.com", "a plus, as used for sub-addressing")]
    public void AllowedUserNameCharacters_AdmitsAddressesPeopleActuallyHave(string address, string because)
    {
        // Arrange
        var allowed = AllowedCharacters();

        // Act
        var rejected = address.Where(c => !allowed.Contains(c)).ToList();

        // Assert
        rejected.Should().BeEmpty($"an address containing {because} has to be usable as a username");
    }

    [Fact]
    public void AllowedUserNameCharacters_IsTheAddressGrammar()
    {
        // Arrange & Act — not "contains the same characters" but "is the same value". A username here is
        // an address, so the two cannot be allowed to diverge by a character.
        var allowed = AllowedCharacters();

        // Assert
        allowed.Should().Be(EmailAddress.AllowedCharacters);
    }

    [Fact]
    public void AllowedCharacters_AgreesWithWhatTheFormatCheckActuallyAccepts()
    {
        // Arrange — the constant and the regex behind IsValidEmailAddressFormat are two statements of one
        // grammar, written apart. This is what stops them drifting: every character the constant permits
        // has to survive being put in a real address and validated.
        var localPartCharacters = EmailAddress.AllowedCharacters.Replace("@", string.Empty).Replace(".", string.Empty);

        // Act
        var rejected = localPartCharacters
            .Where(c => !$"a{c}b@example.com".IsValidEmailAddressFormat())
            .ToList();

        // Assert
        rejected.Should().BeEmpty();
    }

    [Fact]
    public void AllowedCharacters_OmitsNothingTheFormatCheckAccepts()
    {
        // Arrange — the other direction. Without this, narrowing the constant goes unnoticed: the set
        // stays a subset of what the regex allows, so every "is it permitted" test still passes while an
        // address the system calls valid can no longer become an account. That is the exact shape of the
        // apostrophe bug this branch fixed.
        var printableAscii = Enumerable.Range(33, 94).Select(i => (char)i);

        // Act
        var missing = printableAscii
            .Where(c => $"a{c}b@example.com".IsValidEmailAddressFormat())
            .Where(c => !EmailAddress.AllowedCharacters.Contains(c))
            .ToList();

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public void AllowedUserNameCharacters_StillExcludesWhitespace()
    {
        // Arrange & Act — whitespace is legal in a quoted local part, and still excluded on purpose: a
        // username has to survive being compared, normalized and stored as a single token.
        var allowed = AllowedCharacters();

        // Assert
        allowed.Should().NotContain(" ");
        allowed.Should().NotContain("\t");
    }
}
