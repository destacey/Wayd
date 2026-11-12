using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayd.Common.Domain.Authorization;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.Validation;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Holds a request body's FluentValidation validator to running before the action, through the real MVC pipeline:
/// an invalid body answers 422 problem details keyed by property path, and a valid one reaches the action.
/// </summary>
/// <remarks>
/// The API's request validators mostly repeat their command's rules, which the dispatch pipeline also answers with
/// a 422 of the same shape, so the response alone cannot show which layer rejected a body. The registration test
/// pins the API layer; the HTTP tests pin the response a client reads.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class RequestValidationTests(WaydSqlServerApiFactory factory)
{
    private const string ScoringModels = "/api/scoring-models";

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Post_AnswersUnprocessableEntityKeyedByPropertyPath_WhenTheBodyFailsItsValidator()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var user = await _factory.CreateAuthenticatedClient(CreateScoringModels);

        // Act
        var response = await user.Client.PostAsJsonAsync(ScoringModels, new
        {
            name = "",
            description = "Validated before the action runs",
            criteria = new[]
            {
                new { name = "Value", token = "BV" },
                new { name = "", token = "not a token" },
            },
        }, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var errors = body.RootElement.GetProperty("errors").EnumerateObject().Select(e => e.Name).ToArray();
        errors.Should().BeEquivalentTo("Name", "Criteria[1].Name", "Criteria[1].Token");
    }

    [Fact]
    public async Task Post_ReachesTheAction_WhenTheBodyPassesItsValidator()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var user = await _factory.CreateAuthenticatedClient(CreateScoringModels);

        // Act
        var response = await user.Client.PostAsJsonAsync(ScoringModels, new
        {
            name = $"Validated {Guid.NewGuid():N}",
            description = "Passes its validator",
            criteria = new[] { new { name = "Value", token = "BV" } },
        }, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public void Host_RegistersTheRequestValidationFilterAheadOfTheModelStateCheck()
    {
        // Arrange
        var filters = _factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value.Filters;

        // Act
        var registered = filters.OfType<TypeFilterAttribute>()
            .Where(f => f.ImplementationType == typeof(RequestValidationFilter))
            .ToArray();

        // Assert
        registered.Should().ContainSingle().Which.Order.Should().Be(RequestValidationFilter.Order);
    }

    private static string CreateScoringModels =>
        ApplicationPermission.NameFor(ApplicationAction.Create, ApplicationResource.ScoringModels);
}
