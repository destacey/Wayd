using FluentAssertions;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Commands;

public sealed class ImportProjectDeliverablesCommandValidatorTests
{
    private readonly ImportProjectDeliverablesCommandValidator _validator = new();

    private static SubmittedImportRow<ImportProjectDeliverableDto> Row(string importId, string projectKey, string name) =>
        new(importId, new ImportProjectDeliverableDto(new ProjectKey(projectKey), name, null, null, null, null, null, null, null));

    [Fact]
    public async Task Validate_WhenANameRepeatsWithinAProjectIgnoringCase_NamesTheProjectAndTheName()
    {
        // Arrange
        var command = new ImportProjectDeliverablesCommand([Row("r1", "APOLLO", "Portal"), Row("r2", "APOLLO", " portal ")]);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("'Portal' on project APOLLO");
    }

    [Fact]
    public async Task Validate_WhenTheSameNameIsOnDifferentProjects_IsValid()
    {
        // Arrange
        var command = new ImportProjectDeliverablesCommand([Row("r1", "APOLLO", "Portal"), Row("r2", "GEMINI", "Portal")]);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
