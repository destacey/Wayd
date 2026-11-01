using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Planning.Application.EstimationScales.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.EstimationScales.Queries;

public class GetEstimationScaleQueryHandlerTests : IDisposable
{
    private static readonly Instant Published = Instant.FromUtc(2026, 2, 1, 12, 0);

    private readonly FakePlanningDbContext _dbContext = new();

    private GetEstimationScaleQueryHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Handle_ShouldReturnTheCurrentVersionAndEveryVersionNewestFirst()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithValues("S", "M", "?").Generate();
        scale.PublishVersion([new EstimationScaleValue("S", true), new EstimationScaleValue("M", true), new EstimationScaleValue("L", true)], EventActor.System, Published);
        _dbContext.AddEstimationScale(scale);
        _dbContext.AddEstimationScale(new EstimationScaleFaker().Generate());

        // Act
        var result = await CreateHandler().Handle(new GetEstimationScaleQuery(scale.Id.ToString()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(scale.Id);
        result.Key.Should().Be(scale.Key);
        result.CurrentVersion.Number.Should().Be(2);
        result.CurrentVersion.PublishedOn.Should().Be(Published);
        result.CurrentVersion.Values.Select(v => (v.Value, v.IsEstimate)).Should().Equal(("S", true), ("M", true), ("L", true));
        result.Versions.Select(v => v.Number).Should().Equal(2, 1);
        result.Versions[1].Values.Select(v => (v.Value, v.IsEstimate)).Should().Equal(("S", true), ("M", true), ("?", false));
    }

    [Fact]
    public async Task Handle_ByKey_ShouldReturnTheScale()
    {
        // Arrange
        var scale = new EstimationScaleFaker().WithKey(42).Generate();
        _dbContext.AddEstimationScale(scale);

        // Act
        var result = await CreateHandler().Handle(new GetEstimationScaleQuery("42"), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(scale.Id);
    }

    [Fact]
    public async Task Handle_UnknownScale_ShouldReturnNull()
    {
        // Arrange
        _dbContext.AddEstimationScale(new EstimationScaleFaker().Generate());

        // Act
        var result = await CreateHandler().Handle(new GetEstimationScaleQuery(Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
