namespace Wayd.Web.Api.Controllers.UserManagement;

[Route("api/user-management/permissions")]
[ApiVersionNeutral]
[ApiController]
public class PermissionsController : ControllerBase
{
    public PermissionsController()
    {
    }

    [HttpGet]
    [MustHaveAnyPermission(
        "Permissions.View",
        "Permissions.Roles.View",
        "Permissions.Users.View")]
    [EndpointSummary("Get a list of all permissions.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<ApplicationPermission>> GetList()
    {
        var permissions = ApplicationPermissions.All;

        return Ok(permissions);
    }
}

