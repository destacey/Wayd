using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeDeskTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));

    #region Create

    [Fact]
    public void Create_WhenValid_SetsEverySetting()
    {
        // Arrange
        var reviewerId = Guid.NewGuid();

        // Act
        var sut = IntakeDesk.Create(" IT ", " Laptops, access and internal tools ", IntakeDeskVisibility.SummaryOnly,
            isRequesterSelectable: true, isOpen: false, requesterMayAddParticipants: false, [reviewerId],
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Name.Should().Be("IT");
        sut.Description.Should().Be("Laptops, access and internal tools");
        sut.Visibility.Should().Be(IntakeDeskVisibility.SummaryOnly);
        sut.IsRequesterSelectable.Should().BeTrue();
        sut.IsOpen.Should().BeFalse();
        sut.RequesterMayAddParticipants.Should().BeFalse();
        sut.Reviewers.Should().ContainSingle().Which.Should().BeEquivalentTo(new { DeskId = sut.Id, EmployeeId = reviewerId });
    }

    [Fact]
    public void Create_WithARepeatedReviewer_NamesThemOnce()
    {
        // Arrange
        var reviewerId = Guid.NewGuid();

        // Act
        var sut = IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [reviewerId, reviewerId], EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Reviewers.Should().ContainSingle();
    }

    [Fact]
    public void Create_WithAnEmptyReviewerId_Throws()
    {
        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [Guid.Empty], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("  ", "Internal tools")]
    [InlineData("IT", "  ")]
    public void Create_WithABlankNameOrDescription_Throws(string name, string description)
    {
        // Act
        Action act = () => IntakeDesk.Create(name, description, IntakeDeskVisibility.Open, true, true, true,
            [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnUndefinedVisibility_Throws()
    {
        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", (IntakeDeskVisibility)99, true, true, true,
            [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RaisesCreatedEventOnlyOnceKeyed_WithTheDeskAsCreated()
    {
        // Arrange
        var reviewerId = Guid.NewGuid();
        var timestamp = _dateTimeProvider.Now;

        // Act
        var sut = IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Private, false, true, false,
            [reviewerId], EventActor.System, timestamp);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.Should().ContainSingle().Which();

        var created = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskCreatedEvent>().Subject;
        created.Id.Should().Be(sut.Id);
        created.Name.Should().Be("IT");
        created.Visibility.Should().Be(IntakeDeskVisibility.Private);
        created.IsRequesterSelectable.Should().BeFalse();
        created.IsOpen.Should().BeTrue();
        created.RequesterMayAddParticipants.Should().BeFalse();
        created.ReviewerIds.Should().Equal(reviewerId);
        created.Timestamp.Should().Be(timestamp);
    }

    #endregion Create

    #region IsReviewer

    [Fact]
    public void IsReviewer_ForANamedReviewer_IsTrue()
    {
        // Arrange
        var reviewerId = Guid.NewGuid();
        var sut = new IntakeDeskFaker().WithReviewers(reviewerId).Generate();

        // Act
        var result = sut.IsReviewer(reviewerId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsReviewer_ForSomeoneNotNamed_IsFalse()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithReviewers(Guid.NewGuid()).Generate();

        // Act
        var result = sut.IsReviewer(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion IsReviewer
}
