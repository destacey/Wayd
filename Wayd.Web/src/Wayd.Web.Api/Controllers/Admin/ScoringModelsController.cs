using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.Common.Application.Scoring.ScoringModels.Queries;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Admin.ScoringModels;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/scoring-models")]
[ApiVersionNeutral]
[ApiController]
public class ScoringModelsController(ILogger<ScoringModelsController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<ScoringModelsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Get a list of scoring models.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ScoringModelListDto>>> GetScoringModels([FromQuery] ScoringModelState? state, CancellationToken cancellationToken)
    {
        var models = await _dispatcher.Send(new GetScoringModelsQuery(state), cancellationToken);

        return Ok(models);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Get scoring model details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScoringModelDetailsDto>> GetScoringModel(string idOrKey, CancellationToken cancellationToken)
    {
        var model = await _dispatcher.Send(new GetScoringModelQuery(idOrKey), cancellationToken);

        return model is not null
            ? Ok(model)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Create a scoring model.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateScoringModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateScoringModelCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScoringModel), new { idOrKey = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Update a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateScoringModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateScoringModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Delete a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteScoringModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Activate a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateScoringModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Archive a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveScoringModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/evaluate")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Evaluate (test) a scoring model against supplied criterion values.", "")]
    [ProducesResponseType(typeof(ScoringModelEvaluationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScoringModelEvaluationDto>> Evaluate(Guid id, [FromBody] EvaluateScoringModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToQuery(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #region Criteria

    [HttpPost("{id}/criteria")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Add a criterion to a scoring model.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> AddCriterion(Guid id, [FromBody] ScoringModelCriterionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScoringModel), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/criteria/{criterionId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Update a criterion in a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateCriterion(Guid id, Guid criterionId, [FromBody] ScoringModelCriterionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateCommand(id, criterionId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/criteria/{criterionId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Remove a criterion from a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveCriterion(Guid id, Guid criterionId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveScoringModelCriterionCommand(id, criterionId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/criteria/reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Reorder criteria in a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderCriteria(Guid id, [FromBody] ReorderScoringModelCriteriaRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderScoringModelCriteriaCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Criteria

    #region Scales

    [HttpPost("{id}/scales")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Add a rating scale to a scoring model.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> AddScale(Guid id, [FromBody] ScoringScaleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScoringModel), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/scales/{scaleId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Rename a rating scale in a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateScale(Guid id, Guid scaleId, [FromBody] ScoringScaleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateCommand(id, scaleId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/scales/{scaleId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Remove a rating scale from a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveScale(Guid id, Guid scaleId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveScoringScaleCommand(id, scaleId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/scales/reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Reorder rating scales in a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderScales(Guid id, [FromBody] ReorderScoringScalesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderScoringScalesCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Scales

    #region Scale Levels

    [HttpPost("{id}/scales/{scaleId}/levels")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Add a rating level to a scale.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> AddScaleLevel(Guid id, Guid scaleId, [FromBody] ScoringScaleLevelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddCommand(id, scaleId), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScoringModel), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/scales/{scaleId}/levels/{levelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Update a rating level on a scale.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateScaleLevel(Guid id, Guid scaleId, Guid levelId, [FromBody] ScoringScaleLevelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateCommand(id, scaleId, levelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/scales/{scaleId}/levels/{levelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Remove a rating level from a scale.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveScaleLevel(Guid id, Guid scaleId, Guid levelId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveScoringScaleLevelCommand(id, scaleId, levelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/scales/{scaleId}/levels/reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Reorder rating levels on a scale.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderScaleLevels(Guid id, Guid scaleId, [FromBody] ReorderScoringScaleLevelsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderScoringScaleLevelsCommand(id, scaleId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Scale Levels

    #region Outputs

    [HttpPost("{id}/outputs")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Add an output formula to a scoring model.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> AddOutput(Guid id, [FromBody] ScoringModelOutputRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScoringModel), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/outputs/{outputId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Update an output formula in a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateOutput(Guid id, Guid outputId, [FromBody] ScoringModelOutputRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateCommand(id, outputId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/outputs/{outputId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Remove an output formula from a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveOutput(Guid id, Guid outputId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveScoringModelOutputCommand(id, outputId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/outputs/reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ScoringModels)]
    [OpenApiOperation("Reorder output formulas in a scoring model.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderOutputs(Guid id, [FromBody] ReorderScoringModelOutputsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderScoringModelOutputsCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Outputs
}
