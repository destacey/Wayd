using FluentAssertions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Authorization;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class GetImportDefinitionsQueryHandlerTests
{
    private static readonly string _employeePermission =
        ApplicationPermission.NameFor(ApplicationAction.Import, ApplicationResource.Employees);

    private static readonly string _viewAllPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Imports);

    private readonly Mock<ICurrentPrincipal> _principal = new();

    private readonly TestImportDefinition _employees = new(new ImportPayloadSerializer());

    private readonly TestImportDefinition _teams = new(new ImportPayloadSerializer())
    {
        KeyOverride = "team-import",
        DisplayNameOverride = "A Team Import",
        PermissionResourceOverride = ApplicationResource.Teams,
    };

    private GetImportDefinitionsQueryHandler CreateHandler() =>
        new(new ImportDefinitionRegistry([_employees, _teams]), _principal.Object);

    private Task<IReadOnlyList<Application.Imports.Dtos.ImportDefinitionDto>> Get() =>
        CreateHandler().Handle(new GetImportDefinitionsQuery(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ReturnsOnlyWhatTheCallerMaySubmit()
    {
        // Arrange
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _principal.Setup(p => p.HasPermission(_employeePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await Get();

        // Assert
        result.Select(d => d.Key).Should().Equal(_employees.Key);
    }

    [Fact]
    public async Task Handle_CarriesTheLimitsSoAFormCanSayNoBeforeTheUpload()
    {
        // Arrange
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await Get();

        // Assert
        var definition = result.Single(d => d.Key == _employees.Key);
        definition.MaxRows.Should().Be(_employees.MaxRows);
        definition.InlineThreshold.Should().Be(_employees.InlineThreshold);
    }

    [Fact]
    public async Task Handle_OrdersByDisplayNameForThePicker()
    {
        // Arrange
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await Get();

        // Assert
        result.Select(d => d.DisplayName).Should().Equal("A Team Import", "Test Import");
    }

    [Fact]
    public async Task Handle_ForACallerPermittedOnNothing_ReturnsEmpty()
    {
        // Arrange
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await Get();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForAnOversightHolder_ReturnsEveryTypeMarkedAsNotSubmittable()
    {
        // Arrange — the list widens to what they may see; an upload form must not offer these
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _principal.Setup(p => p.HasPermission(_viewAllPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await Get();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.CanSubmit.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_ForAnOversightHolderWhoAlsoSubmits_FlagsOnlyWhatTheySubmit()
    {
        // Arrange
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _principal.Setup(p => p.HasPermission(_viewAllPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _principal.Setup(p => p.HasPermission(_employeePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await Get();

        // Assert
        result.Single(d => d.Key == _employees.Key).CanSubmit.Should().BeTrue();
        result.Single(d => d.Key == _teams.Key).CanSubmit.Should().BeFalse();
    }
}
