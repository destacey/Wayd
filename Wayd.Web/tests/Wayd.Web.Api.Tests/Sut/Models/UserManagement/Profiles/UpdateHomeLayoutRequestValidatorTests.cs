using FluentValidation.TestHelper;
using Wayd.Common.Application.Identity.Users;
using Wayd.Web.Api.Models.UserManagement.Profiles;

namespace Wayd.Web.Api.Tests.Sut.Models.UserManagement.Profiles;

public sealed class UpdateHomeLayoutRequestValidatorTests
{
    private readonly UpdateHomeLayoutRequestValidator _validator = new();

    private static HomeLayoutWidgetRequest Widget(string id, HomeLayoutColumn column = HomeLayoutColumn.Main) =>
        new() { WidgetId = id, Column = column, Visible = true };

    [Fact]
    public void ValidLayout_Passes()
    {
        // Arrange
        var request = new UpdateHomeLayoutRequest
        {
            Widgets = [Widget("my-projects"), Widget("following", HomeLayoutColumn.Side)],
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NoWidgets_Passes()
    {
        // Arrange
        var request = new UpdateHomeLayoutRequest { Widgets = [] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NullWidgetList_Fails()
    {
        // Arrange
        var request = new UpdateHomeLayoutRequest { Widgets = null! };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Widgets);
    }

    [Fact]
    public void NullWidgetEntry_Fails()
    {
        // Arrange
        var request = new UpdateHomeLayoutRequest { Widgets = [Widget("my-projects"), null!] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Widgets[1]");
        result.ShouldNotHaveValidationErrorFor("Widgets[0]");
    }

    [Theory]
    [InlineData(UpdateHomeLayoutRequest.MaxWidgets, false)]
    [InlineData(UpdateHomeLayoutRequest.MaxWidgets + 1, true)]
    public void WidgetCount_IsCapped(int count, bool fails)
    {
        // Arrange
        var request = new UpdateHomeLayoutRequest
        {
            Widgets = [.. Enumerable.Range(0, count).Select(i => Widget($"widget-{i}"))],
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        if (fails)
            result.ShouldHaveValidationErrorFor(r => r.Widgets);
        else
            result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("a", false)]
    [InlineData(null, true)]
    public void WidgetId_IsRequired(string? id, bool fails)
    {
        // Arrange
        var request = new UpdateHomeLayoutRequest { Widgets = [Widget(id!)] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        if (fails)
            result.ShouldHaveValidationErrorFor("Widgets[0].WidgetId");
        else
            result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(UpdateHomeLayoutRequest.MaxWidgetIdLength, false)]
    [InlineData(UpdateHomeLayoutRequest.MaxWidgetIdLength + 1, true)]
    public void WidgetId_LengthIsCapped(int length, bool fails)
    {
        // Arrange
        var request = new UpdateHomeLayoutRequest { Widgets = [Widget(new string('w', length))] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        if (fails)
            result.ShouldHaveValidationErrorFor("Widgets[0].WidgetId");
        else
            result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UndefinedColumn_Fails()
    {
        // Arrange
        var request = new UpdateHomeLayoutRequest
        {
            Widgets = [Widget("my-projects"), Widget("following", (HomeLayoutColumn)7)],
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Widgets[1].Column");
        result.ShouldNotHaveValidationErrorFor("Widgets[0].Column");
    }
}
