using System.Collections.ObjectModel;

namespace Wayd.Common.Domain.Authorization;

public static class ApplicationRoles
{
    public const string Admin = nameof(Admin);

    public static IReadOnlyList<string> DefaultRoles { get; } = new ReadOnlyCollection<string>(new[]
    {
        Admin
    });

    public static bool IsDefault(string roleName) => DefaultRoles.Any(r => r == roleName);
}