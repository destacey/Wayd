using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.SprintMetrics.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.SprintMetrics;

public class UpdateSprintMetricsSettingsCommandHandlerTests
{
    private readonly Mock<ISystemSettingsStore> _store = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public UpdateSprintMetricsSettingsCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("admin-1");
        _currentUser.Setup(u => u.GetEmployeeId()).Returns(_employeeId);
    }

    private UpdateSprintMetricsSettingsCommandHandler CreateHandler() =>
        new(_store.Object, _currentUser.Object, NullLogger<UpdateSprintMetricsSettingsCommandHandler>.Instance);

    [Fact]
    public async Task Handle_SavesTheValuesAsTheCurrentUser()
    {
        // Arrange
        _store.Setup(s => s.Save(It.IsAny<SprintMetricsSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await CreateHandler().Handle(new UpdateSprintMetricsSettingsCommand(5), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _store.Verify(s => s.Save(
            new SprintMetricsSettings { SprintWindow = 5 },
            EventActor.User("admin-1", _employeeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsTheStoresFailure()
    {
        // Arrange
        _store.Setup(s => s.Save(It.IsAny<SprintMetricsSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("'Sprint Window' must be between 3 and 12."));

        // Act
        var result = await CreateHandler().Handle(new UpdateSprintMetricsSettingsCommand(40), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between 3 and 12");
    }
}
