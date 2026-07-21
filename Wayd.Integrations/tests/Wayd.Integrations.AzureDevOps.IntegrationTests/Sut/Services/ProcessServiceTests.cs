using Microsoft.Extensions.Logging;
using Wayd.Integrations.AzureDevOps.IntegrationTests.Models;
using Wayd.Integrations.AzureDevOps.Services;
using Moq;

namespace Wayd.Integrations.AzureDevOps.IntegrationTests.Sut.Services;

[Collection("OptionsCollection")]
public class ProcessServiceTests
{
    private readonly AzdoOrganizationOptions _azdoOrganizationOptions;
    private readonly ProcessServiceData _processServiceData;

    private readonly Mock<ILogger<ProcessService>> _mockLogger;

    public ProcessServiceTests(OptionsFixture fixture)
    {
        _azdoOrganizationOptions = fixture.AzdoOrganizationOptions;
        _processServiceData = fixture.ProcessServiceData;

        _mockLogger = new Mock<ILogger<ProcessService>>();
    }

    [Fact]
    public async Task GetProcesses_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var expectedCount = _processServiceData.ProcessListCount;
        var expectedLogMessage = $"{expectedCount} processes found.";

        var service = new ProcessService(
            new HttpClient(),
            _azdoOrganizationOptions.OrganizationUrl,
            _azdoOrganizationOptions.PersonalAccessToken,
            _azdoOrganizationOptions.ApiVersion,
            _mockLogger.Object);

        // Act
        var result = await service.GetProcesses(TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().NotBeEmpty();
        result.Value.Count.Should().Be(expectedCount);
    }

    [Fact]
    public async Task GetProcess_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var processId = _processServiceData.GetProcessId;
        var expectedLogMessage = $"Process {processId} found.";
        var expectedBacklogLevelsCount = _processServiceData.GetProcessBacklogLevelsCount;

        var service = new ProcessService(
            new HttpClient(),
            _azdoOrganizationOptions.OrganizationUrl,
            _azdoOrganizationOptions.PersonalAccessToken,
            _azdoOrganizationOptions.ApiVersion,
            _mockLogger.Object);

        // Act
        var result = await service.GetProcess(processId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.WorkTypeLevels.Should().NotBeNull();
        result.Value.WorkTypeLevels.Should().NotBeEmpty();
        result.Value.WorkTypeLevels.Count().Should().Be(expectedBacklogLevelsCount);
    }

    [Fact]
    public async Task GetProcess_WithInvalidProcessId_ReturnsFailure()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var expectedErrorMessage = "Not Found";
        var expectedLogMessage = $"Error getting process {processId} from Azure DevOps: Not Found.";

        var service = new ProcessService(
            new HttpClient(),
            _azdoOrganizationOptions.OrganizationUrl,
            _azdoOrganizationOptions.PersonalAccessToken,
            _azdoOrganizationOptions.ApiVersion,
            _mockLogger.Object);

        // Act
        var result = await service.GetProcess(processId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Be(expectedErrorMessage);

        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => string.Equals(v.ToString(), expectedLogMessage)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
            Times.Once);
    }

    [Fact]
    public async Task GetProcess_WithInvalidOranizationUrl_ReturnsFailure()
    {
        // Arrange
        var organizationUrl = "https://www.test12345678.com";
        var processId = _processServiceData.GetProcessId;
        var expectedErrorMessage1 = "Connection Error - The SSL connection could not be established, see inner exception.";
        var expectedErrorMessage2 = "Connection Error - A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond. (www.test12345678.com:443)";
        var expectedErrorMessage3 = "Connection Error - No connection could be made because the target machine actively refused it. (www.test12345678.com:443)";
        var expectedErrorMessage4 = "Connection Error - The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch";

        var service = new ProcessService(
            new HttpClient(),
            organizationUrl,
            _azdoOrganizationOptions.PersonalAccessToken,
            _azdoOrganizationOptions.ApiVersion,
            _mockLogger.Object);

        // Act
        var result = await service.GetProcess(processId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().BeOneOf(expectedErrorMessage1, expectedErrorMessage2, expectedErrorMessage3, expectedErrorMessage4);

        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
            Times.Once);
    }

    [Fact]
    public async Task GetProcess_WithInvalidPersonalAccessToken_ReturnsFailure()
    {
        // Arrange
        var personalAccessToken = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var processId = _processServiceData.GetProcessId;
        var expectedErrorMessage = "Unauthorized";
        var expectedLogMessage = $"Error getting process {processId} from Azure DevOps: Unauthorized.";

        var service = new ProcessService(
            new HttpClient(),
            _azdoOrganizationOptions.OrganizationUrl,
            personalAccessToken,
            _azdoOrganizationOptions.ApiVersion,
            _mockLogger.Object);

        // Act
        var result = await service.GetProcess(processId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Be(expectedErrorMessage);

        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => string.Equals(v.ToString(), expectedLogMessage)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
            Times.Once);
    }
}
