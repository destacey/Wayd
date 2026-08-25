namespace System.Security.Claims;

public static class ClaimsPrincipalExtensions
{
    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Email);

    public static string? GetEmployeeId(this ClaimsPrincipal principal)
        => principal.FindFirstValue("EmployeeId");

    public static string? GetFullName(this ClaimsPrincipal principal)
        => principal?.FindFirst(ApplicationClaims.Fullname)?.Value;

    public static string? GetFirstName(this ClaimsPrincipal principal)
        => principal?.FindFirst(ClaimTypes.Name)?.Value;

    public static string? GetSurname(this ClaimsPrincipal principal)
        => principal?.FindFirst(ClaimTypes.Surname)?.Value;

    /// <summary>
    /// Composes "First Last" from the first-name and surname claims, both of which Entra and the
    /// Wayd JWT emit. Distinct from <see cref="GetFullName"/>, which reads a single fullname claim
    /// that only some providers supply.
    /// <para>
    /// Deliberately not named GetDisplayName: Microsoft.Identity.Web ships an extension of that
    /// name, and matching it makes every call site ambiguous.
    /// </para>
    /// </summary>
    /// <returns>
    /// The composed name, the first name alone when there is no surname, or <c>null</c> when there
    /// is no first name — a lone surname is ignored rather than rendered as a name missing its
    /// start.
    /// </returns>
    public static string? GetComposedName(this ClaimsPrincipal? principal)
    {
        var firstName = principal?.GetFirstName();
        if (string.IsNullOrWhiteSpace(firstName))
            return null;

        var surname = principal?.GetSurname();
        return string.IsNullOrWhiteSpace(surname)
            ? firstName.Trim()
            : $"{firstName.Trim()} {surname.Trim()}";
    }

    public static string? GetPhoneNumber(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.MobilePhone);

    public static string? GetUserId(this ClaimsPrincipal principal)
       => principal.FindFirstValue(ClaimTypes.NameIdentifier);

    //public static string? GetObjectId(this ClaimsPrincipal principal) 
    //   => principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

    public static DateTimeOffset GetExpiration(this ClaimsPrincipal principal) =>
        DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(
            principal.FindFirstValue(ApplicationClaims.Expiration)));

    private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType) =>
        principal is null
            ? throw new ArgumentNullException(nameof(principal))
            : principal.FindFirst(claimType)?.Value;
}