using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Tests.Sut.Persistence;

/// <summary>
/// The negative cases. The positive one needs a real provider's exception, which has no public
/// constructor, so it is a Testcontainers test: <c>DbUpdateExceptionExtensionsIntegrationTests</c>.
/// </summary>
public sealed class DbUpdateExceptionExtensionsTests
{
    [Fact]
    public void IsUniqueViolation_IsFalse_WhenThereIsNoInnerException()
    {
        // Arrange
        var exception = new DbUpdateException("save failed");

        // Act & Assert
        exception.IsUniqueViolation().Should().BeFalse();
    }

    [Fact]
    public void IsUniqueViolation_IsFalse_WhenTheInnerExceptionIsNotTheProviders()
    {
        // Arrange
        var exception = new DbUpdateException("save failed", new TimeoutException("the server did not respond"));

        // Act & Assert
        exception.IsUniqueViolation().Should().BeFalse();
    }
}
