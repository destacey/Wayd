using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.Employees.Queries;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// The employees an intake request can name, for anyone who works with requests.
/// </summary>
[Route("api/demand-management/employee-options")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
[McpTools(McpToolset.Intake, "wayd-intake")]
public class IntakeEmployeeOptionsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [Authorize]
    [McpTool("IntakeEmployeeOptions_GetList", "List employees a request can name")]
    [EndpointSummary("Get the active employees a request can name, by name only.")]
    [EndpointDescription("For choosing a requester, a participant or whoever made a commitment, without the View permission on employees.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeNavigationDto>>> GetEmployeeOptions(CancellationToken cancellationToken)
    {
        var employees = await _dispatcher.Send(new GetIntakeEmployeeOptionsQuery(), cancellationToken);
        return Ok(employees);
    }
}
