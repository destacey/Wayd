using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Queries;

public class GetProductActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetProductActivitiesQueryHandler _handler;
    private readonly ProductFaker _productFaker = new();

    public GetProductActivitiesQueryHandlerTests()
    {
        _handler = new GetProductActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenProductDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetProductActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsActivities_WhenProductExists()
    {
        // Arrange
        var product = _productFaker.Generate();
        _dbContext.AddProduct(product);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = 1,
                EventType = "ProductAddedEvent",
                DomainArea = "ProductManagement",
                AggregateType = "Product",
                AggregateId = product.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Product Added"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(product.Id, "Product", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetProductActivitiesQuery(new IdOrKey(product.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task Handle_NamesTheProductARelatedEntryWasRaisedOn()
    {
        // Arrange
        var parent = _productFaker.Generate();
        var child = _productFaker.Generate();
        _dbContext.AddProduct(parent);
        _dbContext.AddProduct(child);

        var own = Activity(parent.Id, isRelated: false);
        var moved = Activity(child.Id, isRelated: true);

        _activityLogReader
            .Setup(r => r.Read(parent.Id, "Product", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ActivityLogDto>([moved, own], 2, 1, 50));

        // Act
        var result = await _handler.Handle(
            new GetProductActivitiesQuery(new IdOrKey(parent.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        var items = result.Value!.Items;
        items.Single(i => i.Id == moved.Id).RaisedOn.Should().BeEquivalentTo(new { child.Id, child.Key, child.Name });
        items.Single(i => i.Id == own.Id).RaisedOn.Should().BeNull("an entry raised on this product needs no pointer back to it");
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_LeavesRaisedOnEmpty_WhenTheRelatedProductNoLongerExists()
    {
        // Arrange
        var parent = _productFaker.Generate();
        _dbContext.AddProduct(parent);

        var fromRemoved = Activity(Guid.CreateVersion7(), isRelated: true);

        _activityLogReader
            .Setup(r => r.Read(parent.Id, "Product", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ActivityLogDto>([fromRemoved], 1, 1, 50));

        // Act
        var result = await _handler.Handle(
            new GetProductActivitiesQuery(new IdOrKey(parent.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        var item = result.Value!.Items.Should().ContainSingle().Subject;
        item.IsRelated.Should().BeTrue();
        item.RaisedOn.Should().BeNull();
    }

    private static ActivityLogDto Activity(Guid aggregateId, bool isRelated) => new()
    {
        Id = 1,
        EventType = "ProductReparentedEventV2",
        DomainArea = "ProductManagement",
        AggregateType = "Product",
        AggregateId = aggregateId,
        ActorKind = EventActorKind.User,
        Timestamp = Instant.FromUnixTimeSeconds(100),
        Payload = "{}",
        Summary = "Product Reparented",
        IsRelated = isRelated,
    };

    public void Dispose() => _dbContext.Dispose();
}
