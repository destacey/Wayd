using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Planning.Application.EstimationScales.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.EstimationScales.Commands;

public class UpdateEstimationScaleCommandHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30);

    private readonly FakePlanningDbContext _dbContext;
    private readonly UpdateEstimationScaleCommandHandler _handler;
    private readonly Mock<ILogger<UpdateEstimationScaleCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));

    public UpdateEstimationScaleCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdateEstimationScaleCommandHandler>>();

        _handler = new UpdateEstimationScaleCommandHandler(_dbContext, _currentUser.Object, _dateTimeProvider, _mockLogger.Object);
    }

    private static List<EstimationScaleValueInput> SizesWith(params string[] estimates) =>
        [.. estimates.Select(e => new EstimationScaleValueInput(e, true)), new("?", false)];

    [Fact]
    public async Task Handle_ChangedValues_ShouldPublishTheNextVersion()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithName("Sizes").WithDescription(null).WithValues("S", "M", "L", "?").Generate();
        _dbContext.AddEstimationScale(scale);
        var original = scale.CurrentVersion;

        var command = new UpdateEstimationScaleCommand(scale.Id, "Sizes", null, SizesWith("S", "M", "L", "XL"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        scale.Versions.Should().HaveCount(2);
        scale.CurrentVersion.Number.Should().Be(2);
        scale.CurrentVersion.PublishedOn.Should().Be(Now);
        scale.CurrentVersion.Values.Select(v => v.Value).Should().Equal("S", "M", "L", "XL", "?");
        original.Values.Select(v => v.Value).Should().Equal("S", "M", "L", "?");
        scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleVersionPublishedEvent>()
            .Which.PreviousVersionId.Should().Be(original.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnchangedValues_ShouldPublishNothing()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithName("Sizes").WithDescription(null).WithValues("S", "M", "L", "?").Generate();
        _dbContext.AddEstimationScale(scale);

        var command = new UpdateEstimationScaleCommand(scale.Id, "Sizes", null, SizesWith("S", "M", "L"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        scale.Versions.Should().ContainSingle();
        scale.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RenameOnly_ShouldUpdateTheDetailsAndPublishNoVersion()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithName("Sizes").WithDescription("Old").WithValues("S", "M", "L", "?").Generate();
        _dbContext.AddEstimationScale(scale);

        var command = new UpdateEstimationScaleCommand(scale.Id, "T-Shirt Sizes", "New", SizesWith("S", "M", "L"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        scale.Name.Should().Be("T-Shirt Sizes");
        scale.Description.Should().Be("New");
        scale.Versions.Should().ContainSingle();
        scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleDetailsUpdatedEvent>()
            .Which.Previous.Should().Be(new EstimationScaleDetails("Sizes", "Old"));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenScaleDoesNotExist()
    {
        // Arrange
        var command = new UpdateEstimationScaleCommand(Guid.NewGuid(), "Name", null, SizesWith("1", "2"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenFewerThanTwoEstimates()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("1", "2", "?").Generate();
        _dbContext.AddEstimationScale(scale);

        var command = new UpdateEstimationScaleCommand(scale.Id, scale.Name, null, SizesWith("1"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An estimation scale must have at least 2 estimate values.");
        scale.Versions.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
