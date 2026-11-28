using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.AppIntegration.Application.Connections.ExternalSystems;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves against a real database what an in-memory fake cannot: two requests that each create the same new outside
/// system collide on the unique index, and the loser is told to try again rather than failing with an error.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ExternalSystemLinkerTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 9, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = InstallSecretProtector(fixture);

    /// <summary>
    /// Connections encrypt their configuration on save, through a protector the host installs at startup. Installed
    /// here only when nothing has, so a protector another test set is never swapped from under it.
    /// </summary>
    private static SqlServerDbContextFixture InstallSecretProtector(SqlServerDbContextFixture fixture)
    {
        try
        {
            _ = SecretProtectorAccessor.Current;
        }
        catch (InvalidOperationException)
        {
            SecretProtectorAccessor.Set(new AesGcmSecretProtector(RandomNumberGenerator.GetBytes(32)));
        }

        return fixture;
    }

    [Fact]
    public async Task Save_AsksForAnotherTry_WhenAnotherRequestSavedTheSameNewSystemFirst()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var tenantId = $"tenant-{Guid.NewGuid():N}";
        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var firstLinker = new ExternalSystemLinker(first, Mock.Of<IAzureDevOpsUrlResolver>());
        var secondLinker = new ExternalSystemLinker(second, Mock.Of<IAzureDevOpsUrlResolver>());
        var firstConnection = Entra(tenantId);
        var secondConnection = Entra(tenantId);
        (await firstLinker.Link(firstConnection, EventActor.System, Now, ct)).IsSuccess.Should().BeTrue();
        (await secondLinker.Link(secondConnection, EventActor.System, Now, ct)).IsSuccess.Should().BeTrue();
        first.Connections.Add(firstConnection);
        second.Connections.Add(secondConnection);
        (await firstLinker.Save(ct)).IsSuccess.Should().BeTrue();

        // Act
        var result = await secondLinker.Save(ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ExternalSystemLinker.ConcurrentCreateMessage);
        await using var verify = _fixture.CreateContext();
        (await verify.ExternalSystems.CountAsync(s => s.SystemId == tenantId, ct)).Should().Be(1);
    }

    private static EntraConnection Entra(string tenantId) =>
        EntraConnection.Create("People", null, new EntraConnectionConfiguration(tenantId, "client-1", "secret"), true, EventActor.System, Now);
}
