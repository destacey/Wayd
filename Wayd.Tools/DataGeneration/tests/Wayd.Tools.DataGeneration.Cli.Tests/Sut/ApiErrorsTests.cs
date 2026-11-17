using System.Text;
using FluentAssertions;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using Wayd.Tools.DataGeneration.Cli.Client.Models;
using Wayd.Tools.DataGeneration.Cli.Seeding;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

public class ApiErrorsTests
{
    /// <summary>Reads a body the way the generated client does when the API refuses a call.</summary>
    private static async Task<T> Refusal<T>(string json, int status, ParsableFactory<T> factory) where T : ApiException, IParsable
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var node = await new JsonParseNodeFactory().GetRootParseNodeAsync("application/json", stream, TestContext.Current.CancellationToken);
        var problem = node.GetObjectValue(factory)!;
        problem.ResponseStatusCode = status;
        return problem;
    }

    [Fact]
    public async Task Describe_NamesEachValidationErrorTheApiReturned()
    {
        // Arrange
        var refusal = await Refusal(
            """{"title":"One or more validation errors occurred.","status":422,"errors":{"Email":["'Email' is already in use.","'Email' is too long."]}}""",
            422,
            HttpValidationProblemDetails.CreateFromDiscriminatorValue);

        // Act
        var description = ApiErrors.Describe(refusal);

        // Assert
        description.Should().Be("422 One or more validation errors occurred. Email: 'Email' is already in use. 'Email' is too long.");
    }

    [Fact]
    public async Task Describe_GivesTheTitleAndDetailOfAProblem()
    {
        // Arrange
        var refusal = await Refusal(
            """{"title":"Forbidden","detail":"The role 'Admin' cannot be assigned.","status":403}""",
            403,
            ProblemDetails.CreateFromDiscriminatorValue);

        // Act
        var description = ApiErrors.Describe(refusal);

        // Assert
        description.Should().Be("403 Forbidden The role 'Admin' cannot be assigned.");
    }

    [Fact]
    public void Describe_FallsBackToTheStatusWhenTheBodyIsNotAProblem()
    {
        // Arrange
        var refusal = new ApiException("The server returned an unexpected status code.") { ResponseStatusCode = 500 };

        // Act
        var description = ApiErrors.Describe(refusal);

        // Assert
        description.Should().Be("500: The server returned an unexpected status code.");
    }
}
