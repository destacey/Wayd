using FluentAssertions;
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

    private static Dictionary<string, string> Settings(int count) =>
        Enumerable.Range(0, count).ToDictionary(i => $"setting-{i}", _ => "value");

    [Fact]
    public void WidgetWithSettings_Passes()
    {
        // Arrange
        var widget = Widget("my-projects");
        widget.Settings = new() { ["rows"] = "10" };
        var request = new UpdateHomeLayoutRequest { Widgets = [widget, Widget("following")] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(UpdateHomeLayoutRequest.MaxWidgetSettings, false)]
    [InlineData(UpdateHomeLayoutRequest.MaxWidgetSettings + 1, true)]
    public void SettingCount_IsCapped(int count, bool fails)
    {
        // Arrange
        var widget = Widget("my-projects");
        widget.Settings = Settings(count);
        var request = new UpdateHomeLayoutRequest { Widgets = [widget] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        if (fails)
            result.ShouldHaveValidationErrorFor("Widgets[0].Settings");
        else
            result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(UpdateHomeLayoutRequest.MaxSettingKeyLength, false)]
    [InlineData(UpdateHomeLayoutRequest.MaxSettingKeyLength + 1, true)]
    public void SettingKey_MustBePresentAndBounded(int length, bool fails)
    {
        // Arrange
        var widget = Widget("my-projects");
        widget.Settings = new() { [new string('k', length)] = "value" };
        var request = new UpdateHomeLayoutRequest { Widgets = [widget] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        if (fails)
            result.ShouldHaveValidationErrorFor("Widgets[0].Settings");
        else
            result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(UpdateHomeLayoutRequest.MaxSettingValueLength, false)]
    [InlineData(UpdateHomeLayoutRequest.MaxSettingValueLength + 1, true)]
    public void SettingValue_IsBounded(int length, bool fails)
    {
        // Arrange
        var widget = Widget("my-projects");
        widget.Settings = new() { ["rows"] = new string('v', length) };
        var request = new UpdateHomeLayoutRequest { Widgets = [widget] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        if (fails)
            result.ShouldHaveValidationErrorFor("Widgets[0].Settings");
        else
            result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BlankSettingKey_Fails()
    {
        // Arrange
        var widget = Widget("my-projects");
        widget.Settings = new() { ["  "] = "value" };
        var request = new UpdateHomeLayoutRequest { Widgets = [widget] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Widgets[0].Settings");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void SettingsLengthAcrossTheLayout_IsCapped(int over, bool fails)
    {
        // Arrange — every widget within its own limits, so only the total can fail
        const string key = "k";
        var perWidget = key.Length + UpdateHomeLayoutRequest.MaxSettingValueLength;
        var full = UpdateHomeLayoutRequest.MaxSettingsLength / perWidget;
        var remainder = UpdateHomeLayoutRequest.MaxSettingsLength - (full * perWidget) + over;
        var lengths = Enumerable.Repeat(UpdateHomeLayoutRequest.MaxSettingValueLength, full)
            .Append(remainder - key.Length);
        var request = new UpdateHomeLayoutRequest
        {
            Widgets =
            [
                .. lengths.Select((length, i) =>
                {
                    var widget = Widget($"widget-{i}");
                    widget.Settings = new() { [key] = new string('v', length) };
                    return widget;
                }),
            ],
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        if (fails)
            result.ShouldHaveValidationErrorFor(r => r.Widgets);
        else
            result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NullSettingValue_Fails()
    {
        // Arrange
        var widget = Widget("my-projects");
        widget.Settings = new() { ["rows"] = null! };
        var request = new UpdateHomeLayoutRequest { Widgets = [widget] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Widgets[0].Settings");
    }

    [Fact]
    public void ToUserHomeLayoutDto_CarriesSettings_AndReadsNoneAsEmpty()
    {
        // Arrange
        var tuned = Widget("my-projects");
        tuned.Settings = new() { ["rows"] = "10" };
        var request = new UpdateHomeLayoutRequest { Widgets = [tuned, Widget("following")] };

        // Act
        var layout = request.ToUserHomeLayoutDto();

        // Assert
        layout.Widgets[0].Settings.Should().Equal(new Dictionary<string, string> { ["rows"] = "10" });
        layout.Widgets[1].Settings.Should().BeEmpty();
    }
}
