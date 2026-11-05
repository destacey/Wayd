using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Seeding;
using Wayd.Tools.DataGeneration.Cli.Client;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>Which desks the seed gives an estimation scale, and which it can estimate requests on.</summary>
public class WaydSeedClientTests
{
    private static readonly Guid DeskId = Guid.NewGuid();

    private static readonly IntakeEstimationScaleChoiceDto TShirtSizes = new()
    {
        Id = Guid.NewGuid(),
        Key = 3,
        Name = "T-Shirt Sizes",
        VersionNumber = 2,
        EstimateValues = ["S", "M", "L"],
    };

    private static IntakeDeskModel Desk(string? scaleName = "T-Shirt Sizes") => new()
    {
        Name = "IT Desk",
        Description = "Internal systems.",
        Visibility = "Open",
        Members = [],
        EstimationScaleName = scaleName,
    };

    private static IReadOnlyList<IntakeDeskEstimationPlan> Plan(
        IntakeDeskModel desk, EstimationScaleVersionNavigationDto? pinned, params IntakeEstimationScaleChoiceDto[] choices) =>
        WaydSeedClient.PlanIntakeDeskEstimationScales(
            [desk],
            new Dictionary<string, Guid> { [desk.Name] = DeskId },
            choices,
            new Dictionary<Guid, EstimationScaleVersionNavigationDto?> { [DeskId] = pinned });

    private static EstimationScaleVersionNavigationDto PinnedTo(Guid scaleId, bool isCurrentVersion = true) => new()
    {
        Id = scaleId,
        Key = 9,
        Name = "Pinned",
        VersionId = Guid.NewGuid(),
        VersionNumber = 1,
        IsCurrentVersion = isCurrentVersion,
    };

    [Fact]
    public void PlanIntakeDeskEstimationScales_GivesAnUnpinnedDeskItsScale()
    {
        // Act
        var plans = Plan(Desk(), pinned: null, TShirtSizes);

        // Assert
        plans.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new IntakeDeskEstimationPlan("IT Desk", DeskId, TShirtSizes.Id, ["S", "M", "L"]));
    }

    [Fact]
    public void PlanIntakeDeskEstimationScales_EstimatesADeskAlreadyOnTheScalesCurrentVersionWithoutChangingIt()
    {
        // Act
        var plans = Plan(Desk(), PinnedTo(TShirtSizes.Id), TShirtSizes);

        // Assert
        plans.Should().ContainSingle().Which.PinScaleId.Should().BeNull();
    }

    [Fact]
    public void PlanIntakeDeskEstimationScales_LeavesOutADeskOnAnotherScaleOrAnOlderVersion()
    {
        // Act
        var onAnother = Plan(Desk(), PinnedTo(Guid.NewGuid()), TShirtSizes);
        var onAnOlderVersion = Plan(Desk(), PinnedTo(TShirtSizes.Id, isCurrentVersion: false), TShirtSizes);

        // Assert — its values are not the ones the choices list, so the import would refuse them
        onAnother.Should().BeEmpty();
        onAnOlderVersion.Should().BeEmpty();
    }

    [Fact]
    public void PlanIntakeDeskEstimationScales_LeavesOutADeskThatDoesNotEstimateOrWhoseScaleIsNotOffered()
    {
        // Act
        var notEstimating = Plan(Desk(scaleName: null), pinned: null, TShirtSizes);
        var notOffered = Plan(Desk(scaleName: "Fibonacci"), pinned: null, TShirtSizes);

        // Assert
        notEstimating.Should().BeEmpty();
        notOffered.Should().BeEmpty();
    }
}
