using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.ProductTags.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.ProductTags;

public class UpdateProductTagSettingsCommandHandlerTests
{
    private readonly Mock<ISystemSettingsStore> _store = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public UpdateProductTagSettingsCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("admin-1");
        _currentUser.Setup(u => u.GetEmployeeId()).Returns(_employeeId);
    }

    private UpdateProductTagSettingsCommandHandler CreateHandler() =>
        new(_store.Object, _currentUser.Object, NullLogger<UpdateProductTagSettingsCommandHandler>.Instance);

    [Fact]
    public async Task Handle_SavesTheValuesAsTheCurrentUser()
    {
        // Arrange
        _store.Setup(s => s.Save(It.IsAny<ProductTagSettings>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await CreateHandler().Handle(new UpdateProductTagSettingsCommand(true), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _store.Verify(s => s.Save(
            new ProductTagSettings { AllowsFreeTags = true },
            EventActor.User("admin-1", _employeeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
