using Microsoft.AspNetCore.Identity;

namespace Wayd.Infrastructure.Identity;

/// <summary>
/// Keeps the administrators from all being locked out at once. An administrator with no other active administrator
/// able to sign in, because there is none or every other is locked out, is never locked out: nobody could unlock them,
/// and anyone who knew the user names could otherwise keep every administrator locked out in turn. Their wrong
/// passwords are throttled instead.
/// </summary>
/// <remarks>
/// The throttle reuses <see cref="ApplicationUser.LockoutEnd"/> as the time before which no attempt is checked, and
/// takes an attempt by saving that time before the password is checked. The save fails on the concurrency stamp for
/// every request but one, so guesses sent in parallel are not checked in parallel.
/// </remarks>
internal static class LastAdministrator
{
    /// <summary>The longest an attempt waits after the one before it failed.</summary>
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether <paramref name="user"/> is an active administrator whom no other active administrator could unlock,
    /// because each other is locked out at <paramref name="now"/> or there is none.
    /// </summary>
    /// <remarks>An administrator is an active user holding every permission; see <see cref="Administrators"/>.</remarks>
    public static async Task<bool> IsProtected(
        UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, ApplicationUser user, DateTimeOffset now)
    {
        if (!await Administrators.IsAdministrator(userManager, roleManager, user, CancellationToken.None))
            return false;

        var administrators = await Administrators.Load(userManager, roleManager, CancellationToken.None);
        return administrators.Active(u => u.Id == user.Id || u.LockoutEnd > now).Count == 0;
    }

    /// <summary>
    /// Takes the next attempt at <paramref name="user"/>'s password, answering false when it may not be checked: one
    /// is due no earlier than <see cref="ApplicationUser.LockoutEnd"/>, or another request took it first. Past the
    /// lockout threshold the attempt is taken by saving the time the one after it may come.
    /// </summary>
    /// <remarks>
    /// A hold longer than <see cref="MaxDelay"/> is a lockout from before the user was protected, which no longer
    /// applies to them, so it does not stop the attempt.
    /// </remarks>
    public static async Task<bool> TryTakeAttempt(UserManager<ApplicationUser> userManager, ApplicationUser user, DateTimeOffset now)
    {
        if (user.LockoutEnd > now && user.LockoutEnd <= now + MaxDelay)
            return false;

        var threshold = userManager.Options.Lockout.MaxFailedAccessAttempts;
        if (user.AccessFailedCount < threshold)
            return true;

        user.LockoutEnd = now + Delay(user.AccessFailedCount, threshold);
        return (await userManager.UpdateAsync(user)).Succeeded;
    }

    /// <summary>Counts a wrong password against <paramref name="user"/>, and past the threshold holds off the next attempt.</summary>
    public static async Task RecordFailure(UserManager<ApplicationUser> userManager, ApplicationUser user, DateTimeOffset now)
    {
        user.AccessFailedCount++;
        var threshold = userManager.Options.Lockout.MaxFailedAccessAttempts;
        if (user.AccessFailedCount >= threshold)
            user.LockoutEnd = now + Delay(user.AccessFailedCount, threshold);

        await userManager.UpdateAsync(user);
    }

    /// <summary>Clears <paramref name="user"/>'s failures and any hold, after a right password.</summary>
    public static async Task RecordSuccess(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        if (user.AccessFailedCount == 0 && user.LockoutEnd is null)
            return;

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await userManager.UpdateAsync(user);
    }

    /// <summary>
    /// How long the next attempt waits after <paramref name="failures"/> failures: nothing before the lockout
    /// threshold, then one second, doubling with each further failure up to <see cref="MaxDelay"/>.
    /// </summary>
    internal static TimeSpan Delay(int failures, int lockoutThreshold)
    {
        var beyond = failures - lockoutThreshold;
        return beyond < 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Min(Math.Pow(2, beyond), MaxDelay.TotalSeconds));
    }
}
