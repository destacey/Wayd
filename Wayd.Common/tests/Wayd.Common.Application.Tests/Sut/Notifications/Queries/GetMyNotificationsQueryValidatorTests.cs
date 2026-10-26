using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Application.Notifications.Queries;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Queries;

public sealed class GetMyNotificationsQueryValidatorTests
{
    private readonly GetMyNotificationsQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldAcceptTheFirstPage()
    {
        // Act
        var result = _validator.Validate(new GetMyNotificationsQuery(new CursorRequest()));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldAcceptACursorFromAPreviousPage()
    {
        // Arrange
        var before = new NotificationCursor(Instant.FromUtc(2026, 10, 25, 9, 0), "ppm.project", Guid.CreateVersion7()).Encode();

        // Act
        var result = _validator.Validate(new GetMyNotificationsQuery(new CursorRequest(Before: before)));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldRefuseACursorTheApiDidNotHandOut()
    {
        // Act
        var result = _validator.Validate(new GetMyNotificationsQuery(new CursorRequest(Before: "page-2")));

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be(nameof(CursorRequest.Before));
        error.ErrorMessage.Should().Be("'Before' must be a cursor from a previous page.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Validate_ShouldRefuseAPageSizeOutsideTheBounds(int pageSize)
    {
        // Act
        var result = _validator.Validate(new GetMyNotificationsQuery(new CursorRequest(PageSize: pageSize)));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(CursorRequest.PageSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public void Validate_ShouldAcceptAPageSizeAtTheBounds(int pageSize)
    {
        // Act
        var result = _validator.Validate(new GetMyNotificationsQuery(new CursorRequest(PageSize: pageSize)));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
