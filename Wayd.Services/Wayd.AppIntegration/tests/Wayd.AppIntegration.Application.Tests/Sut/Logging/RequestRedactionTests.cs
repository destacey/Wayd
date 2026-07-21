using FluentAssertions;
using Wayd.AppIntegration.Application.Connections.Commands;
using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Commands.Entra;
using Wayd.AppIntegration.Application.Connections.Commands.Workday;
using Wayd.AppIntegration.Application.Logging;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Xunit;

namespace Wayd.AppIntegration.Application.Tests.Sut.Logging;

public class RequestRedactionTests
{
    [Fact]
    public void Redact_CreateAzureDevOpsConnectionCommand_BlanksPersonalAccessToken()
    {
        // Arrange
        var request = new CreateAzureDevOpsConnectionCommand("Acme Connection", null, "acme", "super-secret-pat");

        // Act
        var redacted = request.Redact();

        // Assert
        redacted.ToString().Should().NotContain("super-secret-pat");
        redacted.ToString().Should().Contain("[redacted]");
    }

    [Fact]
    public void Redact_UpdateAzureDevOpsConnectionCommand_BlanksPersonalAccessToken()
    {
        // Arrange
        var request = new UpdateAzureDevOpsConnectionCommand(Guid.NewGuid(), "Acme Connection", null, "acme", "super-secret-pat");

        // Act
        var redacted = request.Redact();

        // Assert
        redacted.ToString().Should().NotContain("super-secret-pat");
    }

    [Fact]
    public void Redact_CreateWorkdayConnectionCommand_BlanksIsuPassword()
    {
        // Arrange
        var request = new CreateWorkdayConnectionCommand(
            "Acme Workday", null, "https://wd.example/wsdl", "isu-user", "super-secret-password",
            WorkdayWorkerKey.EmployeeId, false, EmployeeMatchProperty.Email, false, false, false, null, null);

        // Act
        var redacted = request.Redact();

        // Assert
        redacted.ToString().Should().NotContain("super-secret-password");
    }

    [Fact]
    public void Redact_UpdateWorkdayConnectionCommand_BlanksIsuPassword()
    {
        // Arrange
        var request = new UpdateWorkdayConnectionCommand(
            Guid.NewGuid(), "Acme Workday", null, "https://wd.example/wsdl", "isu-user", "super-secret-password",
            WorkdayWorkerKey.EmployeeId, false, EmployeeMatchProperty.Email, false, false, false, null, null);

        // Act
        var redacted = request.Redact();

        // Assert
        redacted.ToString().Should().NotContain("super-secret-password");
    }

    [Fact]
    public void Redact_CreateEntraConnectionCommand_BlanksClientSecret()
    {
        // Arrange
        var request = new CreateEntraConnectionCommand("Acme Entra", null, "tenant-id", "client-id", "super-secret-client-secret", null, false, EmployeeMatchProperty.Email, false);

        // Act
        var redacted = request.Redact();

        // Assert
        redacted.ToString().Should().NotContain("super-secret-client-secret");
    }

    [Fact]
    public void Redact_UpdateEntraConnectionCommand_BlanksClientSecret()
    {
        // Arrange
        var request = new UpdateEntraConnectionCommand(Guid.NewGuid(), "Acme Entra", null, "tenant-id", "client-id", "super-secret-client-secret", null, false, EmployeeMatchProperty.Email, false);

        // Act
        var redacted = request.Redact();

        // Assert
        redacted.ToString().Should().NotContain("super-secret-client-secret");
    }

    [Fact]
    public void Redact_CreateAzureOpenAIConnectionCommand_BlanksApiKey()
    {
        // Arrange
        var request = new CreateAzureOpenAIConnectionCommand("Acme OpenAI", null, "super-secret-api-key", "gpt-4o", "https://acme.openai.azure.com");

        // Act
        var redacted = request.Redact();

        // Assert
        redacted.ToString().Should().NotContain("super-secret-api-key");
    }

    [Fact]
    public void Redact_UpdateAzureOpenAIConnectionCommand_BlanksApiKey()
    {
        // Arrange
        var request = new UpdateAzureOpenAIConnectionCommand(Guid.NewGuid(), "Acme OpenAI", "https://acme.openai.azure.com", null, "gpt-4o", "super-secret-api-key");

        // Act
        var redacted = request.Redact();

        // Assert
        redacted.ToString().Should().NotContain("super-secret-api-key");
    }
}
