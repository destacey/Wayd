using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves free product tags hold their one rule the aggregates cannot see: a name is unique among them, ignoring
/// case, while staying free to match a tag on an axis.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ProductTagPersistenceTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task SaveChanges_AFreeTag_RoundTripsWithNoAxis()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var free = ProductTag.CreateFree(UniqueName("wave"), EventActor.System, Now).Value;

        // Act
        await using (var context = _fixture.CreateContext())
        {
            context.ProductTags.Add(free);
            await context.SaveChangesAsync(ct);
        }

        // Assert
        await using var reader = _fixture.CreateContext();
        var saved = await reader.ProductTags.AsNoTracking().SingleAsync(t => t.Id == free.Id, ct);
        saved.IsFree.Should().BeTrue();
        saved.Name.Should().Be(free.Name);
    }

    [Fact]
    public async Task SaveChanges_ASecondFreeTagWithTheSameNameInAnotherCase_IsRefused()
    {
        // Arrange — the unique index must treat free tags' empty axes as equal
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName("wave");
        await using (var context = _fixture.CreateContext())
        {
            context.ProductTags.Add(ProductTag.CreateFree(name, EventActor.System, Now).Value);
            await context.SaveChangesAsync(ct);
        }

        // Act
        Func<Task> act = async () =>
        {
            await using var context = _fixture.CreateContext();
            context.ProductTags.Add(ProductTag.CreateFree(name.ToUpperInvariant(), EventActor.System, Now).Value);
            await context.SaveChangesAsync(ct);
        };

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChanges_AFreeTagNamedLikeATagOnAnAxis_IsAllowed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName("ios");
        await using var context = _fixture.CreateContext();
        var category = ProductTagCategory.Create(UniqueName("Axis"), null, allowsMany: true, 1, EventActor.System, Now);
        category.AddTag(name, null, EventActor.System, Now).IsSuccess.Should().BeTrue();
        context.ProductTagCategories.Add(category);
        await context.SaveChangesAsync(ct);

        // Act
        context.ProductTags.Add(ProductTag.CreateFree(name, EventActor.System, Now).Value);
        var act = async () => await context.SaveChangesAsync(ct);

        // Assert
        await act.Should().NotThrowAsync();
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];
}
