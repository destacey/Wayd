using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli;
using Wayd.Tools.DataGeneration.Cli.Ui;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Arguments of a command someone is meant to paste.
/// </summary>
/// <remarks>
/// Both front ends print a command that claims to reproduce the run it describes. An argument that reaches
/// the shell as two — a path with a space, a Windows path whose backslashes bash eats, a password with a
/// <c>$</c> — makes that claim false while looking correct, which is worse than printing nothing.
/// </remarks>
public class ShellArgumentTests
{
    [Theory]
    [InlineData("plain")]
    [InlineData("large-tech")]
    [InlineData("2026-06-15")]
    [InlineData("0.42")]
    public void Quote_LeavesAValueThatNeedsNothing(string value)
    {
        // Arrange & Act & Assert — quoting everything would make the common command harder to read
        ShellArgument.Quote(value).Should().Be(value);
    }

    [Theory]
    [InlineData(@"C:\seed", @"'C:\seed'")]
    [InlineData(@"C:\Program Files\seed", @"'C:\Program Files\seed'")]
    [InlineData("Test123$", "'Test123$'")]
    [InlineData("back`tick", "'back`tick'")]
    public void Quote_WrapsInSingleQuotesWhichBothShellsTakeLiterally(string value, string expected)
    {
        // Arrange & Act & Assert
        ShellArgument.Quote(value).Should().Be(expected);
    }

    [Fact]
    public void Quote_FallsBackToDoubleQuotesForAnApostrophe()
    {
        // Arrange — single quotes cannot carry one, and double quotes are literal in both shells for
        // everything but $ and a backtick
        var quoted = ShellArgument.Quote(@"C:\Dan's seed");

        // Assert
        quoted.Should().Be(@"""C:\Dan's seed""");
    }

    [Fact]
    public void IsPortable_RejectsAValueCarryingBothFormsOfTrouble()
    {
        // Arrange & Act & Assert — bash splices the apostrophe as '\'' and PowerShell doubles it as '',
        // so there is no one form; the page says so rather than printing a command that reproduces
        // something else
        ShellArgument.IsPortable("Te$t'123").Should().BeFalse();
        ShellArgument.IsPortable("Dan's seed").Should().BeTrue();
        ShellArgument.IsPortable("Test123$").Should().BeTrue();
    }

    [Fact]
    public void MetaCharacterClass_IsTheSetThePageApplies()
    {
        // Arrange & Act — the page's rule is the same rule, and it is handed this at compile time rather
        // than repeating the set. This is what that interpolation actually produced.
        // Assert
        UiPage.Html.Should().Contain($"const meta = /[{ShellArgument.MetaCharacterClass}]/;");
    }
}
