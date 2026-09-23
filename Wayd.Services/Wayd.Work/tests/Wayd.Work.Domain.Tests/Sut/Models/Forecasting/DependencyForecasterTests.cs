using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Domain.Tests.Sut.Models.Forecasting;

public class DependencyForecasterTests
{
    private const int HorizonDays = 10;

    private static CompletionForecast Forecast(params int[] trialDays) => new(trialDays, HorizonDays);

    private static ForecastDependency<string> Dependency(string predecessor, string successor) => new(predecessor, successor);

    private static int[] TrialDays(CompletionForecast forecast) =>
        [.. Enumerable.Range(0, forecast.Trials).Select(t => forecast[t])];

    [Fact]
    public void Apply_NoDependencies_KeepsEachItemsOwnForecast()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?> { ["a"] = Forecast(1, 2), ["b"] = Forecast(3, 4) };

        // Act
        var result = DependencyForecaster.Apply(own, []);

        // Assert
        TrialDays(result.Items["a"].Forecast!).Should().Equal(1, 2);
        TrialDays(result.Items["b"].Forecast!).Should().Equal(3, 4);
        result.Dependencies.Should().BeEmpty();
        result.IgnoredDependencies.Should().BeEmpty();
    }

    [Fact]
    public void Apply_SuccessorFinishesNoEarlierThanItsPredecessorInEachTrial()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?> { ["p"] = Forecast(1, 5, 3), ["s"] = Forecast(2, 2, 2) };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("p", "s")]);

        // Assert
        TrialDays(result.Items["s"].Forecast!).Should().Equal(2, 5, 3);
        TrialDays(result.Items["p"].Forecast!).Should().Equal(1, 5, 3);
    }

    [Fact]
    public void Apply_ReportsHowOftenEachDependencySetTheFinish()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?> { ["p"] = Forecast(1, 5, 3, 2), ["s"] = Forecast(2, 2, 2, 2) };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("p", "s")]);

        // Assert — trial 4 ties with the successor's own finish, so waiting cost nothing
        result.Dependencies.Should().ContainSingle()
            .Which.Should().Be(new DependencyInfluence<string>("p", "s", 0.5));
    }

    [Fact]
    public void Apply_DelayCarriesThroughAChain()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?>
        {
            ["a"] = Forecast(9, 1),
            ["b"] = Forecast(1, 1),
            ["c"] = Forecast(2, 2),
        };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("b", "c"), Dependency("a", "b")]);

        // Assert
        TrialDays(result.Items["b"].Forecast!).Should().Equal(9, 1);
        TrialDays(result.Items["c"].Forecast!).Should().Equal(9, 2);
        result.Dependencies.Should().BeEquivalentTo(
        [
            new DependencyInfluence<string>("b", "c", 0.5),
            new DependencyInfluence<string>("a", "b", 0.5),
        ]);
    }

    [Fact]
    public void Apply_SuccessorWaitsForItsLatestPredecessor()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?>
        {
            ["p1"] = Forecast(4, 1),
            ["p2"] = Forecast(1, 6),
            ["s"] = Forecast(2, 2),
        };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("p1", "s"), Dependency("p2", "s")]);

        // Assert
        TrialDays(result.Items["s"].Forecast!).Should().Equal(4, 6);
    }

    [Fact]
    public void Apply_PredecessorBeyondHorizon_KeepsSuccessorBeyondHorizon()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?> { ["p"] = Forecast(HorizonDays + 1, 1), ["s"] = Forecast(2, 2) };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("p", "s")]);

        // Assert
        result.Items["s"].Forecast!.TrialsBeyondHorizon.Should().Be(1);
    }

    [Fact]
    public void Apply_PredecessorWithoutForecast_BlocksEverythingDownstream()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?>
        {
            ["p"] = null,
            ["s"] = Forecast(2, 2),
            ["t"] = Forecast(3, 3),
        };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("p", "s"), Dependency("s", "t")]);

        // Assert
        result.Items["s"].Forecast.Should().BeNull();
        result.Items["s"].BlockedBy.Should().Equal("p");
        result.Items["t"].Forecast.Should().BeNull();
        result.Items["t"].BlockedBy.Should().Equal("p");
        result.Dependencies.Should().OnlyContain(d => d.ShareOfTrialsSettingFinish == null);
    }

    [Fact]
    public void Apply_ItemWithoutForecast_IsNotBlockedBySomethingElse()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?> { ["a"] = null };

        // Act
        var result = DependencyForecaster.Apply(own, []);

        // Assert
        result.Items["a"].Forecast.Should().BeNull();
        result.Items["a"].BlockedBy.Should().BeEmpty();
    }

    [Fact]
    public void Apply_Cycle_IgnoresTheDependencyThatClosesIt()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?>
        {
            ["a"] = Forecast(5, 1),
            ["b"] = Forecast(1, 1),
            ["c"] = Forecast(1, 1),
            ["d"] = Forecast(1, 1),
        };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("a", "b"), Dependency("b", "c"), Dependency("c", "a"), Dependency("c", "d")]);

        // Assert
        result.IgnoredDependencies.Should().Equal(Dependency("c", "a"));
        result.Dependencies.Select(d => (d.Predecessor, d.Successor)).Should().BeEquivalentTo(
            [("a", "b"), ("b", "c"), ("c", "d")]);
        TrialDays(result.Items["a"].Forecast!).Should().Equal(5, 1);
        TrialDays(result.Items["c"].Forecast!).Should().Equal(5, 1);
        TrialDays(result.Items["d"].Forecast!).Should().Equal(5, 1);
    }

    [Fact]
    public void Apply_Cycle_FollowsChainsFromItemsWithNoPredecessorsFirst()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?>
        {
            ["a"] = Forecast(1),
            ["b"] = Forecast(1),
            ["root"] = Forecast(1),
        };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("a", "b"), Dependency("b", "a"), Dependency("root", "a")]);

        // Assert — walking root → a → b, it is b → a that leads back
        result.IgnoredDependencies.Should().Equal(Dependency("b", "a"));
    }

    [Fact]
    public void Apply_ItemDependingOnItself_IgnoresThatDependency()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?> { ["a"] = Forecast(3) };

        // Act
        var result = DependencyForecaster.Apply(own, [Dependency("a", "a")]);

        // Assert
        result.IgnoredDependencies.Should().Equal(Dependency("a", "a"));
        result.Dependencies.Should().BeEmpty();
        TrialDays(result.Items["a"].Forecast!).Should().Equal(3);
    }

    [Fact]
    public void Apply_DependencyOnAnUnknownItem_Throws()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?> { ["s"] = Forecast(1) };

        // Act
        var act = () => DependencyForecaster.Apply(own, [Dependency("p", "s")]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apply_ForecastsWithDifferentTrialCounts_Throws()
    {
        // Arrange
        var own = new Dictionary<string, CompletionForecast?> { ["a"] = Forecast(1, 2), ["b"] = Forecast(1) };

        // Act
        var act = () => DependencyForecaster.Apply(own, []);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
