using Microsoft.AspNetCore.Authorization;
using Microsoft.Net.Http.Headers;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Attachments.Commands;
using Wayd.Common.Application.Attachments.Dtos;
using Wayd.Common.Application.Attachments.Queries;
using Wayd.Web.Api.Extensions;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// Images placed in markdown fields: uploaded here, then referred to from the markdown as
/// <c>![alt](/api/attachments/{id})</c>.
/// </summary>
/// <remarks>
/// Needs only a signed-in account: an upload is readable by its uploader alone until a save of the record whose
/// markdown refers to it claims it, and afterwards by whoever may read that record, by its own read rule. An image
/// the caller may not read is 404, never 403, so a response never confirms that it exists. Not agent tools: neither
/// body is JSON.
/// </remarks>
[Route("api/attachments")]
[ApiController]
[Authorize]
public class AttachmentsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>How long a browser may reuse an image without asking again. Its bytes never change.</summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpPost]
    [RequestSizeLimit(AttachmentLimits.MaxUploadBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentLimits.MaxUploadBytes + 64 * 1024)]
    [EndpointSummary("Upload an image to place in a markdown field.")]
    [EndpointDescription(
        "Takes one PNG, JPEG, GIF or WebP file of at most 5 MB, detected from its bytes. The image is re-encoded, which turns it upright and removes its metadata, including any location. Returns its `path`, to write in the markdown as `![description](path)`. Until a record whose markdown refers to it is saved, only you can see it, and it is deleted after 24 hours. Returns 400 when the file is not an accepted image or is too large.")]
    [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AttachmentDto>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var result = await _dispatcher.Send(new UploadAttachmentCommand(content, file.Length), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetContent), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get an image placed in a markdown field.")]
    [EndpointDescription(
        "Returns the image's bytes with its media type. Returns 404 when it does not exist or you cannot read the record it belongs to. Responses carry an entity tag and may be cached privately: an image never changes.")]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "image/png", "image/jpeg", "image/gif", "image/webp")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetContent(Guid id, CancellationToken cancellationToken)
    {
        var content = await _dispatcher.Send(new GetAttachmentContentQuery(id), cancellationToken);
        if (content is null)
        {
            return NotFound();
        }

        var headers = Response.GetTypedHeaders();
        headers.CacheControl = new CacheControlHeaderValue { Private = true, MaxAge = CacheLifetime, Extensions = { new("immutable") } };
        // Opened directly from the API, the image is a document of its own; nothing in it may run or be framed.
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(content.Data, content.ContentType, lastModified: null, entityTag: new EntityTagHeaderValue($"\"{content.ETag}\""));
    }
}
