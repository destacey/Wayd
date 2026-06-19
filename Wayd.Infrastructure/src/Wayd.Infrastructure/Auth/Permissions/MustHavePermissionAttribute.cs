using Microsoft.AspNetCore.Authorization;

namespace Wayd.Infrastructure.Auth.Permissions;

public class MustHavePermissionAttribute : AuthorizeAttribute
{
    public MustHavePermissionAttribute(string action, string resource) =>
        Policy = ApplicationPermission.NameFor(action, resource);
}