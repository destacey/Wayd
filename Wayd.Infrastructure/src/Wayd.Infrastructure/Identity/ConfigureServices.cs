using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Infrastructure.Identity;

internal static class ConfigureServices
{
    private const string Letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";

    /// <summary>
    /// The punctuation RFC 5322 permits in an unquoted local part (<c>atext</c>).
    /// </summary>
    /// <remarks>
    /// Written out in the RFC's own order rather than sorted, so it can be compared against the spec by
    /// eye. The apostrophe is the one that matters in practice: it is where surnames like O'Brien land.
    /// </remarks>
    private const string EmailAtext = "!#$%&'*+-/=?^_`{|}~";

    /// <summary>
    /// Every character a Wayd username may contain, which is every character a valid email address may
    /// contain — <c>atext</c>, plus the dot that separates atoms and the @ that separates the parts.
    /// </summary>
    /// <remarks>
    /// <b>A Wayd username is always the user's email address</b>, so this rule and the email validation on
    /// the create-user command have to agree. They are applied a step apart by different components, and a
    /// character one accepts and the other rejects produces a failure nobody can act on: the address is
    /// valid, the account cannot be created, and the message complains about a username the caller never
    /// supplied. Deriving this from the address grammar is what keeps them from drifting.
    /// <para>
    /// Whitespace and quoted local parts are deliberately excluded. Both are legal in an address, but a
    /// username has to survive being compared, normalized and stored as a single token.
    /// </para>
    /// </remarks>
    private const string EmailAddressCharacters = Letters + Digits + EmailAtext + ".@";

    internal static IServiceCollection AddIdentity(this IServiceCollection services) =>
        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = true;

                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.Lockout.AllowedForNewUsers = true;

                    options.User.RequireUniqueEmail = true;
                    options.User.AllowedUserNameCharacters = EmailAddressCharacters;
                })
            .AddEntityFrameworkStores<WaydDbContext>()
            .AddDefaultTokenProviders()
            .Services;
}