using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Identity;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Only the provider can raise the exception the helper classifies, so the positive case runs against
/// real SQL Server. A second provider adds its own case here.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DbUpdateExceptionExtensionsIntegrationTests(SqlServerDbContextFixture fixture)
{
    [Fact]
    public async Task IsUniqueViolation_IsTrue_ForADuplicateOnAUniqueIndex()
    {
        // Arrange — two users with the same normalized name, which Identity indexes uniquely
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = $"DUP-{Guid.NewGuid():N}";
        await using var context = fixture.CreateContext();
        context.Users.AddRange(NewUser(name), NewUser(name));

        // Act
        var act = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        var failure = await act.Should().ThrowAsync<DbUpdateException>();
        var inner = failure.Which.InnerException;
        failure.Which.IsUniqueViolation().Should().BeTrue(
            "the provider threw {0}: {1}", inner?.GetType().FullName, inner?.Message);
    }

    private static ApplicationUser NewUser(string normalizedName) => new()
    {
        Id = $"user-{Guid.NewGuid():N}",
        UserName = normalizedName.ToLowerInvariant(),
        NormalizedUserName = normalizedName,
        Email = $"{normalizedName.ToLowerInvariant()}@acme.example",
        NormalizedEmail = $"{normalizedName}@ACME.EXAMPLE",
        SecurityStamp = Guid.NewGuid().ToString(),
        IsActive = true,
        LoginProvider = LoginProviders.Wayd,
    };
}
