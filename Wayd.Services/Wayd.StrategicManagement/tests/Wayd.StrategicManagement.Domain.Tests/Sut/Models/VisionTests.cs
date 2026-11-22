using FluentAssertions;
using Wayd.Common.Models;
using Wayd.StrategicManagement.Domain.Enums;
using Wayd.StrategicManagement.Domain.Models;
using Wayd.StrategicManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.StrategicManagement.Domain.Tests.Sut.Models;

public class VisionTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly VisionFaker _faker;

    public VisionTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new VisionFaker();
    }

    [Fact]
    public void Create_ShouldCreateVisionSuccessfully()
    {
        // Arrange
        var fakeVision = _faker.Generate();

        // Act
        var vision = Vision.Create(fakeVision.Description);

        // Assert
        vision.Should().NotBeNull();
        vision.Description.Should().Be(fakeVision.Description);
        vision.State.Should().Be(VisionState.Proposed);
        vision.Dates.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldUpdateVisionSuccessfully_StateIsProposed()
    {
        // Arrange
        var fakeVision = _faker.Generate();
        var vision = Vision.Create(fakeVision.Description);
        var expectedState = vision.State;
        var expectedDates = vision.Dates;

        string updatedDescription = "Do amazing things.";

        // Act
        var result = vision.Update(updatedDescription);

        // Assert
        result.IsSuccess.Should().BeTrue();
        vision.Description.Should().Be(updatedDescription);
        vision.State.Should().Be(expectedState);
        vision.Dates.Should().Be(expectedDates);
    }

    [Fact]
    public void Update_ShouldUpdateVisionSuccessfully_WhenStateIsActive()
    {
        // Arrange
        var vision = _faker
            .WithState(VisionState.Active)
            .WithDates(new FlexibleInstantRange(_dateTimeProvider.Now))
            .Generate();
        var expectedState = vision.State;
        var expectedDates = vision.Dates;

        string updatedDescription = "Do amazing things.";

        // Act
        var result = vision.Update(updatedDescription);

        // Assert
        result.IsSuccess.Should().BeTrue();
        vision.Description.Should().Be(updatedDescription);
        vision.State.Should().Be(expectedState);
        vision.Dates.Should().Be(expectedDates);
    }

    [Fact]
    public void Update_ShouldFail_WhenStateIsArchived()
    {
        // Arrange
        var vision = _faker
            .WithState(VisionState.Archived)
            .WithDates(new FlexibleInstantRange(_dateTimeProvider.Now.Plus(Duration.FromDays(-10)), _dateTimeProvider.Now))
            .Generate();

        var originalDescription = vision.Description;
        string updatedDescription = "Do amazing things.";

        // Act
        var result = vision.Update(updatedDescription);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The vision is archived and cannot be updated.");
        vision.Description.Should().Be(originalDescription);
    }

    [Fact]
    public void Activate_ShouldActivateVisionSuccessfully()
    {
        // Arrange
        var fakeVision = _faker.Generate();
        var vision = Vision.Create(fakeVision.Description);
        var activationDate = _dateTimeProvider.Now;

        // Act
        var result = vision.Activate(activationDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        vision.State.Should().Be(VisionState.Active);
        vision.Dates.Should().NotBeNull();
        vision.Dates!.Start.Should().Be(activationDate);
        vision.Dates.End.Should().BeNull();
    }

    [Fact]
    public void Activate_ShouldFail_WhenVisionIsNotProposed()
    {
        // Arrange
        var fakeVision = _faker.Generate();
        var vision = Vision.Create(fakeVision.Description);
        var activationDate = _dateTimeProvider.Now;
        vision.Activate(activationDate);

        // Act
        var result = vision.Activate(activationDate.Plus(Duration.FromDays(1)));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("The vision must be in the Proposed state to activate it.");
        vision.State.Should().Be(VisionState.Active);
        vision.Dates.Should().Be(new FlexibleInstantRange(activationDate));
    }

    [Fact]
    public void Archive_ShouldArchiveVisionSuccessfully()
    {
        // Arrange
        var fakeVision = _faker.Generate();
        var vision = Vision.Create(fakeVision.Description);
        var activationDate = _dateTimeProvider.Now.Plus(Duration.FromDays(-10));
        vision.Activate(activationDate);
        var archiveDate = activationDate.Plus(Duration.FromDays(5));

        // Act
        var result = vision.Archive(archiveDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        vision.State.Should().Be(VisionState.Archived);
        vision.Dates.Should().NotBeNull();
        vision.Dates!.Start.Should().Be(activationDate);
        vision.Dates.End.Should().Be(archiveDate);
    }

    [Fact]
    public void Archive_ShouldFail_WhenStateIsProposed()
    {
        // Arrange
        var fakeVision = _faker.Generate();
        var vision = Vision.Create(fakeVision.Description);
        var archiveDate = _dateTimeProvider.Now;

        // Act
        var result = vision.Archive(archiveDate);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("The vision must be in the Active state to archive it.");
        vision.State.Should().Be(VisionState.Proposed);
        vision.Dates.Should().BeNull();
    }

    [Fact]
    public void Archive_ShouldFail_WhenStateIsArchived()
    {
        // Arrange
        var vision = _faker
            .WithState(VisionState.Archived)
            .WithDates(new FlexibleInstantRange(_dateTimeProvider.Now.Plus(Duration.FromDays(-10)), _dateTimeProvider.Now))
            .Generate();
        var originalDates = vision.Dates;
        var archiveDate = _dateTimeProvider.Now.Plus(Duration.FromDays(1));

        // Act
        var result = vision.Archive(archiveDate);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("The vision must be in the Active state to archive it.");
        vision.State.Should().Be(VisionState.Archived);
        vision.Dates.Should().Be(originalDates);
    }

    [Fact]
    public void Archive_ShouldFail_WhenEndIsEarlierThanStart()
    {
        // Arrange
        var vision = _faker
            .WithState(VisionState.Active)
            .WithDates(new FlexibleInstantRange(_dateTimeProvider.Now))
            .Generate();
        var archiveDate = _dateTimeProvider.Now.Plus(Duration.FromDays(-5));

        // Act
        var result = vision.Archive(archiveDate);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("The end date must be on or after the start date.");
        vision.State.Should().Be(VisionState.Active);
        vision.Dates.Should().Be(new FlexibleInstantRange(_dateTimeProvider.Now));
    }
}

