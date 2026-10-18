using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Web.Api.Models.Planning.Roadmaps;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.Roadmaps;

/// <summary>
/// The roadmap item update request as the item patch endpoint builds it from the current item, one request type
/// per item type.
/// </summary>
public sealed class UpdateRoadmapItemRequestTests
{
    private static readonly Guid RoadmapId = Guid.NewGuid();
    private static readonly Guid ParentId = Guid.NewGuid();

    private static readonly RoadmapActivityDetailsDto Activity = new()
    {
        Id = Guid.NewGuid(),
        RoadmapId = RoadmapId,
        Name = "Beta programme",
        Description = "Invite-only beta.",
        Type = SimpleNavigationDto.Create(1, "Activity"),
        Parent = new RoadmapActivityNavigationDto { Id = ParentId, Name = "Launch" },
        Color = "#336699",
        Start = new LocalDate(2026, 5, 1),
        End = new LocalDate(2026, 5, 29),
        Order = 4,
    };

    private static readonly RoadmapMilestoneDetailsDto Milestone = new()
    {
        Id = Guid.NewGuid(),
        RoadmapId = RoadmapId,
        Name = "General availability",
        Description = "Public release.",
        Type = SimpleNavigationDto.Create(2, "Milestone"),
        Parent = new RoadmapActivityNavigationDto { Id = ParentId, Name = "Launch" },
        Color = "#996633",
        Date = new LocalDate(2026, 6, 1),
    };

    private static readonly RoadmapTimeboxDetailsDto Timebox = new()
    {
        Id = Guid.NewGuid(),
        RoadmapId = RoadmapId,
        Name = "Code freeze",
        Description = "No feature merges.",
        Type = SimpleNavigationDto.Create(3, "Timebox"),
        Parent = new RoadmapActivityNavigationDto { Id = ParentId, Name = "Launch" },
        Color = "#669933",
        Start = new LocalDate(2026, 5, 18),
        End = new LocalDate(2026, 5, 29),
    };

    public static TheoryData<RoadmapItemDetailsDto, Type> FullyPopulatedItems => new()
    {
        { Activity, typeof(UpdateRoadmapActivityRequest) },
        { Milestone, typeof(UpdateRoadmapMilestoneRequest) },
        { Timebox, typeof(UpdateRoadmapTimeboxRequest) },
    };

    [Theory]
    [MemberData(nameof(FullyPopulatedItems))]
    public void FromDto_CreatesTheRequestForTheItemsType(RoadmapItemDetailsDto item, Type expectedType)
    {
        // Act
        var request = UpdateRoadmapItemRequest.FromDto(item);

        // Assert
        request.Should().BeOfType(expectedType);
    }

    [Theory]
    [MemberData(nameof(FullyPopulatedItems))]
    public void FromDto_SetsEveryPropertyFromAFullyPopulatedItem(RoadmapItemDetailsDto item, Type _)
    {
        // Act
        var request = UpdateRoadmapItemRequest.FromDto(item);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
    }

    [Theory]
    [MemberData(nameof(FullyPopulatedItems))]
    public void FromDto_MapsTheFieldsWhoseNamesDiffer(RoadmapItemDetailsDto item, Type _)
    {
        // Act
        var request = UpdateRoadmapItemRequest.FromDto(item);

        // Assert
        request.ItemId.Should().Be(item.Id);
        request.RoadmapId.Should().Be(RoadmapId);
        request.ParentId.Should().Be(ParentId);
        request.Color.Should().Be(item.Color);
    }

    [Fact]
    public void FromDto_CarriesTheDatesOfEachItemType()
    {
        // Act
        var activityRequest = (UpdateRoadmapActivityRequest)UpdateRoadmapItemRequest.FromDto(Activity);
        var milestoneRequest = (UpdateRoadmapMilestoneRequest)UpdateRoadmapItemRequest.FromDto(Milestone);
        var timeboxRequest = (UpdateRoadmapTimeboxRequest)UpdateRoadmapItemRequest.FromDto(Timebox);

        // Assert
        activityRequest.Start.Should().Be(Activity.Start);
        activityRequest.End.Should().Be(Activity.End);
        milestoneRequest.Date.Should().Be(Milestone.Date);
        timeboxRequest.Start.Should().Be(Timebox.Start);
        timeboxRequest.End.Should().Be(Timebox.End);
    }

    [Fact]
    public void FromDto_RejectsAnItemOfNoKnownType()
    {
        // Arrange
        var item = new RoadmapItemDetailsDto
        {
            Id = Guid.NewGuid(),
            Name = "Untyped",
            Type = SimpleNavigationDto.Create(9, "Unknown"),
        };

        // Act
        var act = () => UpdateRoadmapItemRequest.FromDto(item);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
