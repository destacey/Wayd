namespace Wayd.Common.Domain.Authorization;

/// <summary>
/// How much of its user's access a token may use: the scope an OAuth access token or a personal access token is
/// limited to. A scope only narrows access; it never grants a permission the user lacks.
/// </summary>
/// <remarks>The values are ordered: each scope includes every scope below it.</remarks>
public enum TokenScope
{
    /// <summary>Read only: permissions whose action views, searches or exports, and nothing that changes data.</summary>
    Read = 1,

    /// <summary>Everything the user may do. Includes <see cref="Read"/>.</summary>
    Write = 2,
}

/// <summary>
/// The OAuth scope strings for <see cref="TokenScope"/> and the rule that applies one to a permission. The same model
/// covers OAuth access tokens and personal access tokens.
/// </summary>
public static class TokenScopes
{
    /// <summary>The scope string for <see cref="TokenScope.Read"/>.</summary>
    public const string Read = "wayd:read";

    /// <summary>The scope string for <see cref="TokenScope.Write"/>.</summary>
    public const string Write = "wayd:write";

    /// <summary>
    /// The claim carrying a token's scope, space-delimited as OAuth writes it (RFC 9068 §2.2.3). A principal without
    /// it is not limited by a scope.
    /// </summary>
    public const string ClaimType = "scope";

    /// <summary>Every scope string, from narrowest to widest, as the metadata documents advertise them.</summary>
    public static IReadOnlyList<string> Supported { get; } = [Read, Write];

    /// <summary>The scope a token gets when it names none: everything its user may do.</summary>
    public const TokenScope Default = TokenScope.Write;

    /// <summary>The permission actions <see cref="TokenScope.Read"/> allows: none of them changes data.</summary>
    private static readonly HashSet<string> _readActions = new(StringComparer.Ordinal)
    {
        ApplicationAction.View,
        ApplicationAction.Search,
        ApplicationAction.Export,
    };

    /// <summary>The scope string for <paramref name="scope"/>.</summary>
    public static string ToScopeString(this TokenScope scope) => scope switch
    {
        TokenScope.Read => Read,
        TokenScope.Write => Write,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown token scope."),
    };

    /// <summary>
    /// The widest scope <paramref name="scopes"/> names, read as a space-delimited OAuth scope parameter; values Wayd
    /// does not define are ignored (RFC 6749 §3.3). Null when it names none.
    /// </summary>
    public static TokenScope? Parse(string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes))
            return null;

        TokenScope? widest = null;
        foreach (var value in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            TokenScope? scope = value switch
            {
                Read => TokenScope.Read,
                Write => TokenScope.Write,
                _ => null,
            };
            if (scope is { } known && (widest is null || known > widest))
                widest = known;
        }

        return widest;
    }

    /// <summary>
    /// Whether a token limited to <paramref name="scope"/> may use <paramref name="permission"/>, a permission name
    /// such as <c>Permissions.WorkItems.View</c>. The user must still hold the permission.
    /// </summary>
    public static bool Permits(this TokenScope scope, string permission) => scope switch
    {
        TokenScope.Write => true,
        TokenScope.Read => _readActions.Contains(ActionOf(permission)),
        _ => false,
    };

    /// <summary>The action of a permission named <c>Permissions.{Resource}.{Action}</c>.</summary>
    private static string ActionOf(string permission)
    {
        var dot = permission.LastIndexOf('.');
        return dot < 0 ? permission : permission[(dot + 1)..];
    }
}
