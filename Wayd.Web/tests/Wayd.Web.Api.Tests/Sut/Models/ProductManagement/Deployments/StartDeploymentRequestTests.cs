using FluentAssertions;
using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.ProductManagement.Deployments;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Deployments;

/// <summary>
/// The deployment an API caller starts, and what it becomes.
/// </summary>
/// <remarks>
/// A deployment carries either a version or a package, never both and never neither. The version field
/// was named <c>ReleaseId</c> after Release and Version were split, so a caller sending
/// <c>versionId</c> bound nothing — and the request was then refused for naming neither, which reads
/// as a validation bug rather than a binding one.
/// </remarks>
public sealed class StartDeploymentRequestTests
{
    private readonly StartDeploymentRequestValidator _validator = new();

    [Fact]
    public void ToStartDeploymentCommand_ShouldCarryTheVersionThrough()
    {
        // Arrange
        var versionId = Guid.CreateVersion7();
        var environmentId = Guid.CreateVersion7();
        var request = new StartDeploymentRequest
        {
            VersionId = versionId,
            EnvironmentId = environmentId,
            ArtifactId = "4.10.0.008",
        };

        // Act
        var command = request.ToStartDeploymentCommand();

        // Assert
        command.VersionId.Should().Be(versionId);
        command.PackageId.Should().BeNull();
        command.EnvironmentId.Should().Be(environmentId);
        command.ArtifactId.Should().Be("4.10.0.008");
    }

    [Fact]
    public void Validate_ShouldAcceptAVersion()
    {
        // Arrange
        var request = new StartDeploymentRequest
        {
            VersionId = Guid.CreateVersion7(),
            EnvironmentId = Guid.CreateVersion7(),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldAcceptAPackage()
    {
        // Arrange
        var request = new StartDeploymentRequest
        {
            PackageId = Guid.CreateVersion7(),
            EnvironmentId = Guid.CreateVersion7(),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldRefuseBoth()
    {
        // Arrange
        // Where a package exists it is the unit that shipped, so naming both would count one shipment
        // twice.
        var request = new StartDeploymentRequest
        {
            VersionId = Guid.CreateVersion7(),
            PackageId = Guid.CreateVersion7(),
            EnvironmentId = Guid.CreateVersion7(),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldRefuseNeither()
    {
        // Arrange
        var request = new StartDeploymentRequest { EnvironmentId = Guid.CreateVersion7() };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
