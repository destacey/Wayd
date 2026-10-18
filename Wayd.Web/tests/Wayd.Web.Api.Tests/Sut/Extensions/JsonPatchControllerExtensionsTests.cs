using CSharpFunctionalExtensions;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.Roadmaps;

namespace Wayd.Web.Api.Tests.Sut.Extensions;

/// <summary>
/// The shared JSON Patch plumbing: a patch applied to the current record as its update request, validated by the
/// request's runtime type, then sent as the full update command. Roadmap milestones stand in for a polymorphic
/// request, whose patch document is declared over the abstract base.
/// </summary>
public sealed class JsonPatchControllerExtensionsTests
{
    private const string NotFoundMessage = "Roadmap item with ID 'x' not found.";

    private static readonly RoadmapMilestoneDetailsDto Milestone = new()
    {
        Id = Guid.NewGuid(),
        RoadmapId = Guid.NewGuid(),
        Name = "General availability",
        Description = "Public release.",
        Type = Wayd.Common.Application.Dtos.SimpleNavigationDto.Create(2, "Milestone"),
        Color = "#996633",
        Date = new LocalDate(2026, 6, 1),
    };

    private readonly List<UpdateRoadmapItemRequest> _sent = [];

    private static JsonPatchDocument<UpdateRoadmapItemRequest> PatchOf(params (string Op, string Path, object? Value)[] operations) =>
        new([.. operations.Select(o => new Operation<UpdateRoadmapItemRequest>(o.Op, o.Path, null, o.Value))], System.Text.Json.JsonSerializerOptions.Web);

    private static PatchingController ControllerWith(bool registerMilestoneValidator = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();
        if (registerMilestoneValidator)
            services.AddSingleton<IValidator<UpdateRoadmapMilestoneRequest>, UpdateRoadmapMilestoneRequestValidator>();

        return new PatchingController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
            },
        };
    }

    private Task<ActionResult> Patch(
        PatchingController controller,
        JsonPatchDocument<UpdateRoadmapItemRequest>? patch,
        RoadmapItemDetailsDto? current = null,
        Result? outcome = null)
    {
        Task<RoadmapItemDetailsDto?> Load(CancellationToken _) => Task.FromResult(current);
        Task<Result> Send(UpdateRoadmapItemRequest request, CancellationToken _)
        {
            _sent.Add(request);
            return Task.FromResult(outcome ?? Result.Success());
        }

        return controller.Patch(patch, Load, NotFoundMessage, UpdateRoadmapItemRequest.FromDto, Send, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Patch_SendsTheCurrentRecordWithOnlyTheNamedFieldsChanged()
    {
        // Arrange
        var controller = ControllerWith();
        var patch = PatchOf(("replace", "/Name", "GA"));

        // Act
        var result = await Patch(controller, patch, Milestone);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var sent = _sent.Should().ContainSingle().Which.Should().BeOfType<UpdateRoadmapMilestoneRequest>().Subject;
        sent.Name.Should().Be("GA");
        sent.Description.Should().Be(Milestone.Description);
        sent.Color.Should().Be(Milestone.Color);
        sent.Date.Should().Be(Milestone.Date);
        sent.ItemId.Should().Be(Milestone.Id);
    }

    [Fact]
    public async Task Patch_ValidatesThePatchedRequestByItsRuntimeType()
    {
        // Arrange — only the milestone validator is registered, none for the abstract base the patch is declared over
        var controller = ControllerWith();
        var patch = PatchOf(("replace", "/Name", ""));

        // Act
        var result = await Patch(controller, patch, Milestone);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        objectResult.Value.Should().BeOfType<ValidationProblemDetails>()
            .Which.Errors.Should().ContainKey(nameof(UpdateRoadmapItemRequest.Name));
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Patch_ReportsPatchAndValidationErrorsTogether()
    {
        // Arrange
        var controller = ControllerWith();
        var patch = PatchOf(("replace", "/NoSuchField", "x"), ("replace", "/Name", ""));

        // Act
        var result = await Patch(controller, patch, Milestone);

        // Assert
        var errors = result.Should().BeOfType<ObjectResult>().Subject
            .Value.Should().BeOfType<ValidationProblemDetails>().Subject.Errors;
        errors.Should().ContainKey(nameof(UpdateRoadmapMilestoneRequest));
        errors.Should().ContainKey(nameof(UpdateRoadmapItemRequest.Name));
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Patch_RefusesARequestTypeWithNoValidator()
    {
        // Arrange
        var controller = ControllerWith(registerMilestoneValidator: false);
        var patch = PatchOf(("replace", "/Name", "GA"));

        // Act
        var act = () => Patch(controller, patch, Milestone);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain(nameof(UpdateRoadmapMilestoneRequest));
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Patch_AnswersNotFoundWhenTheRecordIsMissing()
    {
        // Arrange
        var controller = ControllerWith();
        var patch = PatchOf(("replace", "/Name", "GA"));

        // Act
        var result = await Patch(controller, patch, current: null);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().Be(NotFoundMessage);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Patch_AnswersBadRequestWithoutAPatchDocument()
    {
        // Arrange
        var controller = ControllerWith();

        // Act
        var result = await Patch(controller, patch: null, Milestone);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be("Patch document cannot be null.");
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Patch_AnswersBadRequestWhenTheDomainRefusesTheCommand()
    {
        // Arrange
        var controller = ControllerWith();
        var patch = PatchOf(("replace", "/Name", "GA"));

        // Act
        var result = await Patch(controller, patch, Milestone, outcome: Result.Failure("Roadmap is locked."));

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("Roadmap is locked.");
        _sent.Should().ContainSingle();
    }

    /// <summary>A bare controller to run the extension against.</summary>
    private sealed class PatchingController : ControllerBase;
}
