using Wayd.Common.Models;

namespace Wayd.Common.Tests.Sut.Models;

public class NameCasingTests
{
    // --- Pass-through cases: anything not "mostly uppercase" stays exactly as written ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TitleCaseIfMostlyUpper_nullOrWhitespace_returnsAsIs(string? input)
    {
        var result = NameCasing.TitleCaseIfMostlyUpper(input);
        result.Should().Be(input);
    }

    [Theory]
    [InlineData("Alex")]
    [InlineData("Alex Rivera")]
    [InlineData("d'Artagnan")]
    [InlineData("van der Berg")]
    [InlineData("McKenzie")]
    [InlineData("José")]
    [InlineData("María José")]
    public void TitleCaseIfMostlyUpper_alreadyCased_returnsUnchanged(string input)
    {
        // The whole point of the heuristic is to leave deliberately-cased input alone. If even
        // one of these regresses we'd be silently mangling user data.
        var result = NameCasing.TitleCaseIfMostlyUpper(input);
        result.Should().Be(input);
    }

    // --- Mostly-uppercase cases: get title-cased per the Western-naming rules ---

    [Theory]
    [InlineData("RIVERA", "Rivera")]
    [InlineData("ALEX RIVERA", "Alex Rivera")]
    [InlineData("ALEX JORDAN RIVERA", "Alex Jordan Rivera")]
    public void TitleCaseIfMostlyUpper_simpleAllCaps_titleCases(string input, string expected)
    {
        NameCasing.TitleCaseIfMostlyUpper(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("O'BRIEN", "O'Brien")]
    [InlineData("D'ANGELO", "D'Angelo")]
    [InlineData("L'AMOUR", "L'Amour")]
    public void TitleCaseIfMostlyUpper_apostrophePrefix_capitalizesLetterAfterApostrophe(
        string input, string expected)
    {
        NameCasing.TitleCaseIfMostlyUpper(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("MARY-ANNE", "Mary-Anne")]
    [InlineData("JEAN-LUC", "Jean-Luc")]
    [InlineData("SMITH-JONES", "Smith-Jones")]
    public void TitleCaseIfMostlyUpper_hyphenatedNames_capitalizesEachSegment(
        string input, string expected)
    {
        NameCasing.TitleCaseIfMostlyUpper(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("MCDONALD", "McDonald")]
    [InlineData("MCKENZIE", "McKenzie")]
    [InlineData("MACDONALD", "MacDonald")]
    [InlineData("MACINTYRE", "MacIntyre")]
    [InlineData("Mcdonald", "Mcdonald")] // mixed-case input is pass-through; we don't second-guess
    public void TitleCaseIfMostlyUpper_McMacPrefixes_preserveInnerCapital(string input, string expected)
    {
        NameCasing.TitleCaseIfMostlyUpper(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("MARY ANNE SMITH-JONES", "Mary Anne Smith-Jones")]
    [InlineData("MARY-ANNE O'BRIEN", "Mary-Anne O'Brien")]
    [InlineData("PAT MCDONALD", "Pat McDonald")]
    public void TitleCaseIfMostlyUpper_combinedRules_applyAll(string input, string expected)
    {
        NameCasing.TitleCaseIfMostlyUpper(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("JOSÉ", "José")]
    [InlineData("MÜLLER", "Müller")]
    [InlineData("ÁNGEL GARCÍA-LÓPEZ", "Ángel García-López")]
    public void TitleCaseIfMostlyUpper_unicodeLetters_lowercaseCorrectly(string input, string expected)
    {
        // The heuristic is about case, not script. Latin diacritics and umlauts should all
        // round-trip lower-cased except for the first letter of each word — including across
        // hyphen word boundaries (García-López).
        NameCasing.TitleCaseIfMostlyUpper(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("---")]
    public void TitleCaseIfMostlyUpper_noLetters_returnsUnchanged(string input)
    {
        // No letters means the uppercase ratio is undefined; we treat it as not-mostly-upper and
        // leave the string alone rather than producing weird output.
        NameCasing.TitleCaseIfMostlyUpper(input).Should().Be(input);
    }
}
