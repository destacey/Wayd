namespace Wayd.Tools.DataGeneration.Cli;

/// <summary>
/// Quotes one argument of a command someone is meant to paste into a shell.
/// </summary>
/// <remarks>
/// Both front ends print a command that is supposed to reproduce the run it describes, and an argument
/// that reaches the shell as two — a path with a space in it, a password with a <c>$</c> — reproduces
/// something else instead. Single quotes are literal in bash and PowerShell alike, so they carry every
/// value but one: a value containing an apostrophe takes double quotes, which are literal in both for
/// everything except <c>$</c> and a backtick.
/// <para>
/// A value carrying an apostrophe <em>and</em> one of those has no form that works in both shells — bash
/// splices the apostrophe as <c>'\''</c> and PowerShell doubles it as <c>''</c> — so
/// <see cref="IsPortable"/> exists to say so. A command nobody can paste is better than one that pastes
/// and generates something else.
/// </para>
/// <para>
/// The page applies this same rule in JavaScript. It is handed <see cref="MetaCharacterClass"/> at compile
/// time rather than repeating the set, so the two cannot come to disagree about which characters matter.
/// </para>
/// </remarks>
public static class ShellArgument
{
    /// <summary>
    /// The characters that force quoting, beside whitespace and the backslash.
    /// </summary>
    /// <remarks>
    /// A Windows path carries backslashes, which bash eats unquoted, so it belongs in the set even though
    /// PowerShell would have taken it as it stood.
    /// </remarks>
    private const string Specials = "'\"$`!&|<>();*?";

    /// <summary>The same set as a regular-expression character class, for the page's copy of the rule.</summary>
    public const string MetaCharacterClass = @"\s\\" + Specials;

    public static string Quote(string value) =>
        !NeedsQuoting(value) ? value
        : !value.Contains('\'') ? $"'{value}'"
        : $"\"{value.Replace("\"", "\\\"")}\"";

    /// <summary>Whether the quoted form means the same thing in bash and in PowerShell.</summary>
    public static bool IsPortable(string value) =>
        !value.Contains('\'') || !value.Any(c => c is '$' or '`');

    private static bool NeedsQuoting(string value) =>
        value.Any(c => char.IsWhiteSpace(c) || c == '\\' || Specials.Contains(c));
}
