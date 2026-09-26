using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.Scheduling.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.Scheduling;

public class UpdateSchedulingSettingsCommandHandlerTests
{
    private readonly Mock<ISystemSettingsStore> _store = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public UpdateSchedulingSettingsCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("admin-1");
        _currentUser.Setup(u => u.GetEmployeeId()).Returns(_employeeId);
    }

    private UpdateSchedulingSettingsCommandHandler CreateHandler() =>
        new(_store.Object, _currentUser.Object, NullLogger<UpdateSchedulingSettingsCommandHandler>.Instance);

    [Fact]
    public async Task Handle_SavesTheTrimmedValuesAsTheCurrentUser()
    {
        // Arrange
        _store.Setup(s => s.Save(It.IsAny<SchedulingSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await CreateHandler().Handle(
            new UpdateSchedulingSettingsCommand(" Europe/London ", 3), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _store.Verify(s => s.Save(
            new SchedulingSettings { DefaultTimeZone = "Europe/London", DefaultCommitmentGraceDays = 3 },
            EventActor.User("admin-1", _employeeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsTheStoresFailure()
    {
        // Arrange
        _store.Setup(s => s.Save(It.IsAny<SchedulingSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("'Mars/Olympus_Mons' is not a valid IANA time zone."));

        // Act
        var result = await CreateHandler().Handle(
            new UpdateSchedulingSettingsCommand("Mars/Olympus_Mons", 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a valid IANA time zone");
    }
}
