using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Models;

namespace Wayd.Infrastructure.Identity;

internal static class ConfigureServices
{
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

                    // A Wayd username is always the user's email address, so this takes the address
                    // grammar rather than keeping a list of its own. The two rules are applied a step
                    // apart by different components, and a character one accepts and the other refuses
                    // makes the account impossible to create while complaining about a username the
                    // caller never supplied.
                    options.User.AllowedUserNameCharacters = EmailAddress.AllowedCharacters;
                })
            .AddEntityFrameworkStores<WaydDbContext>()
            .AddDefaultTokenProviders()
            .Services;
}