using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Domain.Tests.Sut.Models.PlanningPoker;

public class EstimationScaleTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 1, 9, 0);
    private static readonly Instant Later = Now.Plus(Duration.FromDays(1));
    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString(), Guid.NewGuid());

    private static EstimationScaleValue[] Fibonacci =>
    [
        new("0", true), new("1", true), new("2", true), new("3", true), new("5", true),
        new("8", true), new("13", true), new("?", false), new("☕", false),
    ];

    private static EstimationScale KeyedScale() =>
        new EstimationScaleFaker().WithName("Sizes").WithDescription("T-shirt").WithValues("S", "M", "L", "?").Generate();

    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldPublishVersionOne()
    {
        // Act
        var result = EstimationScale.Create(" Fibonacci ", "Standard Fibonacci", Fibonacci, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var scale = result.Value;
        scale.Name.Should().Be("Fibonacci");
        scale.Description.Should().Be("Standard Fibonacci");
        scale.IsActive.Should().BeTrue();
        var version = scale.Versions.Should().ContainSingle().Subject;
        scale.CurrentVersion.Should().BeSameAs(version);
        version.Number.Should().Be(1);
        version.EstimationScaleId.Should().Be(scale.Id);
        version.PublishedOn.Should().Be(Now);
        version.Values.Should().Equal(Fibonacci);
    }

    [Fact]
    public void Create_ShouldPreserveOrderAndFlags()
    {
        // Arrange
        EstimationScaleValue[] values = [new("?", false), new("5", true), new("1", true)];

        // Act
        var result = EstimationScale.Create("Test", null, values, Actor, Now);

        // Assert
        result.Value.CurrentVersion.Values.Select(v => (v.Value, v.IsEstimate))
            .Should().Equal(("?", false), ("5", true), ("1", true));
    }

    [Fact]
    public void Create_ShouldRaiseCreatedOnceTheKeyIsAssigned()
    {
        // Arrange
        var scale = EstimationScale.Create("Fibonacci", "Standard", Fibonacci, Actor, Now).Value;
        scale.DomainEvents.Should().BeEmpty();

        // Act
        scale.ExecutePostPersistenceActions();

        // Assert
        var created = scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleCreatedEvent>().Subject;
        created.Id.Should().Be(scale.Id);
        created.Name.Should().Be("Fibonacci");
        created.Description.Should().Be("Standard");
        created.IsActive.Should().BeTrue();
        created.Version.VersionId.Should().Be(scale.CurrentVersion.Id);
        created.Version.Number.Should().Be(1);
        created.Version.Values.Should().Equal(Fibonacci.Select(v => new EstimationScaleValueValues(v.Value, v.IsEstimate)));
        created.Actor.Should().Be(Actor);
        created.Timestamp.Should().Be(Now);
    }

    [Theory]
    [InlineData(new[] { "1" }, new[] { "?", "☕" })]
    [InlineData(new string[0], new[] { "?", "∞" })]
    public void Create_FewerThanTwoEstimates_ShouldReturnFailure(string[] estimates, string[] cards)
    {
        // Arrange
        var values = estimates.Select(v => new EstimationScaleValue(v, true))
            .Concat(cards.Select(c => new EstimationScaleValue(c, false)));

        // Act
        var result = EstimationScale.Create("Test", null, values, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An estimation scale must have at least 2 estimate values.");
    }

    [Fact]
    public void Create_DuplicateValue_ShouldReturnFailure()
    {
        // Arrange
        EstimationScaleValue[] values = [new("1", true), new("2", true), new("1", false)];

        // Act
        var result = EstimationScale.Create("Test", null, values, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The value '1' appears more than once.");
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public void Value_LongerThanTheLimit_ShouldThrow(int length, bool allowed)
    {
        // Act
        var act = () => new EstimationScaleValue($" {new string('x', length)} ", true);

        // Assert
        if (allowed)
            act.Should().NotThrow().Which.Value.Should().HaveLength(length);
        else
            act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_Changed_ShouldRaiseDetailsUpdatedWithBothEnds()
    {
        // Arrange
        var scale = KeyedScale();

        // Act
        var result = scale.UpdateDetails(" Shirts ", " Relative sizing ", Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.Name.Should().Be("Shirts");
        scale.Description.Should().Be("Relative sizing");
        scale.Versions.Should().ContainSingle();
        var updated = scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleDetailsUpdatedEvent>().Subject;
        updated.Key.Should().Be(scale.Key);
        updated.Name.Should().Be("Shirts");
        updated.Description.Should().Be("Relative sizing");
        updated.Previous.Should().Be(new EstimationScaleDetails("Sizes", "T-shirt"));
        updated.Timestamp.Should().Be(Later);
    }

    [Fact]
    public void UpdateDetails_SameAfterTrimming_ShouldRaiseNothing()
    {
        // Arrange
        var scale = KeyedScale();

        // Act
        var result = scale.UpdateDetails(" Sizes", "T-shirt ", Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region PublishVersion

    [Fact]
    public void PublishVersion_ChangedValues_ShouldPublishTheNextVersionAndKeepThePrevious()
    {
        // Arrange
        var scale = KeyedScale();
        var previous = scale.CurrentVersion;
        EstimationScaleValue[] values = [new("Small", true), new("Medium", true), new("Large", true), new("?", false)];

        // Act
        var result = scale.PublishVersion(values, Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.Versions.Should().HaveCount(2);
        var current = scale.CurrentVersion;
        current.Should().NotBeSameAs(previous);
        current.Number.Should().Be(2);
        current.PublishedOn.Should().Be(Later);
        current.Values.Should().Equal(values);
        previous.Values.Select(v => v.Value).Should().Equal("S", "M", "L", "?");

        var published = scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleVersionPublishedEvent>().Subject;
        published.Id.Should().Be(scale.Id);
        published.Key.Should().Be(scale.Key);
        published.Version.VersionId.Should().Be(current.Id);
        published.Version.Number.Should().Be(2);
        published.Version.Values.Should().Equal(values.Select(v => new EstimationScaleValueValues(v.Value, v.IsEstimate)));
        published.PreviousVersionId.Should().Be(previous.Id);
        published.PreviousVersionNumber.Should().Be(1);
    }

    [Fact]
    public void PublishVersion_SameValues_ShouldPublishNothing()
    {
        // Arrange
        var scale = KeyedScale();

        // Act
        var result = scale.PublishVersion([new(" S ", true), new("M", true), new("L", true), new("?", false)], Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.Versions.Should().ContainSingle();
        scale.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("flag")]
    [InlineData("order")]
    public void PublishVersion_OnlyAFlagOrTheOrderChanged_ShouldPublishTheNextVersion(string change)
    {
        // Arrange
        var scale = KeyedScale();
        EstimationScaleValue[] values = change == "flag"
            ? [new("S", true), new("M", true), new("L", false), new("?", false)]
            : [new("L", true), new("M", true), new("S", true), new("?", false)];

        // Act
        var result = scale.PublishVersion(values, Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.CurrentVersion.Number.Should().Be(2);
        scale.CurrentVersion.Values.Should().Equal(values);
    }

    [Fact]
    public void PublishVersion_FewerThanTwoEstimates_ShouldReturnFailure()
    {
        // Arrange
        var scale = KeyedScale();

        // Act
        var result = scale.PublishVersion([new("S", true), new("?", false), new("☕", false)], Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An estimation scale must have at least 2 estimate values.");
        scale.Versions.Should().ContainSingle();
        scale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void PublishVersion_Twice_ShouldNumberEachVersion()
    {
        // Arrange
        var scale = KeyedScale();
        scale.PublishVersion([new("S", true), new("M", true)], Actor, Now);

        // Act
        scale.PublishVersion([new("S", true), new("M", true), new("L", true)], Actor, Later);

        // Assert
        scale.Versions.Select(v => v.Number).Should().BeEquivalentTo([1, 2, 3]);
        scale.CurrentVersion.Number.Should().Be(3);
        scale.DomainEvents.OfType<EstimationScaleVersionPublishedEvent>().Last().PreviousVersionNumber.Should().Be(2);
    }

    #endregion

    #region Activate / Deactivate / Delete

    [Fact]
    public void Deactivate_ShouldRaiseDeactivated()
    {
        // Arrange
        var scale = KeyedScale();

        // Act
        var result = scale.Deactivate(Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.IsActive.Should().BeFalse();
        var deactivated = scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleDeactivatedEvent>().Subject;
        deactivated.Id.Should().Be(scale.Id);
        deactivated.Key.Should().Be(scale.Key);
        scale.Versions.Should().ContainSingle();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldReturnFailure()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithIsActive(false).Generate();

        // Act
        var result = scale.Deactivate(Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already inactive");
        scale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_WhenInactive_ShouldRaiseActivated()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithIsActive(false).Generate();

        // Act
        var result = scale.Activate(Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.IsActive.Should().BeTrue();
        scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleActivatedEvent>()
            .Which.Id.Should().Be(scale.Id);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldReturnFailure()
    {
        // Arrange
        var scale = KeyedScale();

        // Act
        var result = scale.Activate(Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already active");
        scale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_ShouldRaiseDeletedWithTheName()
    {
        // Arrange
        var scale = KeyedScale();

        // Act
        scale.Delete(Actor, Now);

        // Assert
        var deleted = scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleDeletedEvent>().Subject;
        deleted.Id.Should().Be(scale.Id);
        deleted.Key.Should().Be(scale.Key);
        deleted.Name.Should().Be("Sizes");
    }

    #endregion
}
