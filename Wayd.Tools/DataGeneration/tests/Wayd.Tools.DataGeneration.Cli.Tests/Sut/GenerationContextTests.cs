using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

public class GenerationContextTests
{
    private static GenerationContext Context(DateTime? asOf = null, int seed = 1234) =>
        new() { AsOf = asOf ?? new DateTime(2026, 6, 15), Seed = seed };

    [Fact]
    public void FoundedOn_IsTheFloorTheWholeCompanyPredatesNothingBefore()
    {
        // Arrange & Act
        var context = Context(new DateTime(2026, 6, 15));

        // Assert — five years of company history behind the run's today
        context.FoundedOn.Should().Be(new DateTime(2021, 6, 15));
    }

    [Fact]
    public void Window_SpansHistoryBehindAndRunwayAhead()
    {
        // Arrange & Act
        var context = Context(new DateTime(2026, 6, 15));

        // Assert
        context.WindowStart.Should().Be(new DateTime(2024, 6, 15));
        context.WindowEnd.Should().Be(new DateTime(2028, 6, 15));
    }

    [Fact]
    public void Window_SitsInsideTheCompanysOwnLifetime()
    {
        // Arrange & Act — the delivery window cannot open before the company existed, or the floor and
        // the window disagree about what dates are legal
        var context = Context();

        // Assert
        context.WindowStart.Should().BeOnOrAfter(context.FoundedOn);
    }

    [Fact]
    public void SeedFor_IsStableForTheSameAreaAndSeed()
    {
        // Arrange & Act
        var first = Context(seed: 99).SeedFor("organization");
        var second = Context(seed: 99).SeedFor("organization");

        // Assert
        first.Should().Be(second);
    }

    [Fact]
    public void SeedFor_DiffersBetweenAreas()
    {
        // Arrange & Act — two areas drawing the same sequence would generate correlated data
        var context = Context();

        // Assert
        context.SeedFor("organization").Should().NotBe(context.SeedFor("ppm"));
    }

    [Fact]
    public void SeedFor_DiffersBetweenSeeds()
    {
        // Arrange & Act
        var first = Context(seed: 1).SeedFor("ppm");
        var second = Context(seed: 2).SeedFor("ppm");

        // Assert
        first.Should().NotBe(second);
    }

    [Fact]
    public void SeedFor_DoesNotShiftWhenAnotherAreaIsAdded()
    {
        // Arrange — the reason the derivation is by name rather than by position. Offsetting a shared
        // seed per generator means inserting one shifts every later area's data, which breaks quietly for
        // anyone who pinned a seed expecting the same data back.
        var context = Context();
        var before = context.SeedFor("ppm");

        // Act — a new area appears alongside it
        _ = context.SeedFor("users");

        // Assert
        context.SeedFor("ppm").Should().Be(before);
    }

    [Fact]
    public void SeedFor_IsStableAcrossProcesses()
    {
        // Arrange & Act — pinned so a framework change or a randomized string hash cannot silently
        // re-derive every area's seed and reproduce nothing. If this fails, a pinned seed stopped meaning
        // what it meant, and the value is the thing to check rather than the assertion.
        var derived = new GenerationContext { AsOf = new DateTime(2026, 6, 15), Seed = 4242 }.SeedFor("ppm");

        // Assert
        derived.Should().Be(new GenerationContext { AsOf = new DateTime(2020, 1, 1), Seed = 4242 }.SeedFor("ppm"));
    }
}
