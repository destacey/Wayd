using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.WorkItems.Dtos;
using NodaTime;
using Xunit;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Dtos;

public sealed class WorkItemProgressDailyRollupDtoTests
{
    [Theory]
    [InlineData("2023-01-01", "2023-01-01", 1)]
    [InlineData("2023-01-01", "2023-01-05", 5)]
    [InlineData("2023-01-01", "2023-03-05", 64)]
    public void CreateList_ShouldReturnCorrectListSize(string start, string end, int expectedRecordsCount)
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.Parse(start));
        var endDate = DateOnly.FromDateTime(DateTime.Parse(end));

        // Act
        var result = WorkItemProgressDailyRollupDto.CreateList(startDate, endDate, []);

        // Assert
        Assert.Equal(expectedRecordsCount, result.Count);
    }

    [Fact]
    public void CreateList_WithStartDateAfterEndDate_ShouldThrowArgumentException()
    {
        // Arrange
        var startDate = new DateOnly(2023, 1, 6);
        var endDate = new DateOnly(2023, 1, 5);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => WorkItemProgressDailyRollupDto.CreateList(startDate, endDate, []));
    }

    [Fact]
    public void CreateList_WithSingleWorkItem_ShouldReturnCorrectList()
    {
        // Arrange
        var startDate = new DateOnly(2023, 1, 1);
        var endDate = new DateOnly(2023, 1, 15);
        List<WorkItemProgressStateDto> workItems =
        [
            new() {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Requirement,
                StatusCategory = StatusCategory.Done,
                ParentId = null,
                Created = Instant.FromUtc(2023, 1, 2, 12, 15),
                ActivatedTimestamp = Instant.FromUtc(2023, 1, 4, 12, 15),
                DoneTimestamp = Instant.FromUtc(2023, 1, 7, 12, 15)
            },
        ];

        List<WorkItemProgressDailyRollupDto> expectedData =
        [
            WorkItemProgressDailyRollupDto.CreateEmpty(new DateOnly(2023, 1, 1)),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 2), 1, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 3), 1, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 4), 0, 1, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 5), 0, 1, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 6), 0, 1, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 7), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 8), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 9), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 10), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 11), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 12), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 13), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 14), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 15), 0, 0, 1)
        ];

        // Act
        var result = WorkItemProgressDailyRollupDto.CreateList(startDate, endDate, workItems);

        // Assert
        Assert.Equal(expectedData.Count, result.Count);
        for (var i = 0; i < expectedData.Count; i++)
        {
            Assert.Equal(expectedData[i].Date, result[i].Date);
            Assert.Equal(expectedData[i].Proposed, result[i].Proposed);
            Assert.Equal(expectedData[i].Active, result[i].Active);
            Assert.Equal(expectedData[i].Done, result[i].Done);
            Assert.Equal(expectedData[i].Total, result[i].Total);
        }
    }

    [Fact]
    public void CreateList_WithWorkItems_ShouldReturnCorrectList()
    {
        // Arrange
        var startDate = new DateOnly(2023, 1, 1);
        var endDate = new DateOnly(2023, 1, 15);
        List<WorkItemProgressStateDto> workItems =
        [
            new() {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Requirement,
                StatusCategory = StatusCategory.Done,
                ParentId = null,
                Created = Instant.FromUtc(2022, 11, 2, 12, 15),
                ActivatedTimestamp = Instant.FromUtc(2022, 11, 28, 12, 15),
                DoneTimestamp = Instant.FromUtc(2022, 12, 7, 12, 15)
            },
            new() {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Requirement,
                StatusCategory = StatusCategory.Done,
                ParentId = null,
                Created = Instant.FromUtc(2023, 1, 2, 12, 15),
                ActivatedTimestamp = Instant.FromUtc(2023, 1, 4, 12, 15),
                DoneTimestamp = Instant.FromUtc(2023, 1, 7, 12, 15)
            },
            new WorkItemProgressStateDto
            {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Requirement,
                StatusCategory = StatusCategory.Done,
                ParentId = null,
                Created = Instant.FromUtc(2023, 1, 3, 12, 15),
                ActivatedTimestamp = Instant.FromUtc(2023, 1, 6, 12, 15),
                DoneTimestamp = Instant.FromUtc(2023, 1, 8, 12, 15)
            },
            new WorkItemProgressStateDto
            {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Requirement,
                StatusCategory = StatusCategory.Removed,
                ParentId = null,
                Created = Instant.FromUtc(2023, 1, 3, 12, 15),
                DoneTimestamp = Instant.FromUtc(2023, 1, 7, 12, 15)
            },
            new WorkItemProgressStateDto
            {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Requirement,
                StatusCategory = StatusCategory.Proposed,
                ParentId = null,
                Created = Instant.FromUtc(2023, 1, 4, 12, 15),
                DoneTimestamp = null
            },
            new WorkItemProgressStateDto
            {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Requirement,
                StatusCategory = StatusCategory.Active,
                ParentId = null,
                Created = Instant.FromUtc(2023, 1, 9, 12, 15),
                ActivatedTimestamp = Instant.FromUtc(2023, 1, 10, 12, 15),
                DoneTimestamp = null
            },
            new WorkItemProgressStateDto
            {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Requirement,
                StatusCategory = StatusCategory.Done,
                ParentId = null,
                Created = Instant.FromUtc(2023, 1, 10, 12, 15),
                ActivatedTimestamp = Instant.FromUtc(2023, 1, 14, 12, 15), // activated and completed on the same day
                DoneTimestamp = Instant.FromUtc(2023, 1, 14, 12, 15)
            }
        ];

        List<WorkItemProgressDailyRollupDto> expectedData =
        [
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 1), 0, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 2), 1, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 3), 2, 0, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 4), 2, 1, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 5), 2, 1, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 6), 1, 2, 1),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 7), 1, 1, 2),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 8), 1, 0, 3),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 9), 2, 0, 3),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 10), 2, 1, 3),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 11), 2, 1, 3),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 12), 2, 1, 3),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 13), 2, 1, 3),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 14), 1, 1, 4),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 15), 1, 1, 4)
        ];

        // Act
        var result = WorkItemProgressDailyRollupDto.CreateList(startDate, endDate, workItems);

        // Assert
        Assert.Equal(expectedData.Count, result.Count);
        for (var i = 0; i < expectedData.Count; i++)
        {
            Assert.Equal(expectedData[i].Date, result[i].Date);
            Assert.Equal(expectedData[i].Proposed, result[i].Proposed);
            Assert.Equal(expectedData[i].Active, result[i].Active);
            Assert.Equal(expectedData[i].Done, result[i].Done);
            Assert.Equal(expectedData[i].Total, result[i].Total);
        }
    }

    [Fact]
    public void CreateList_WithPortfolioTierItems_ShouldIgnore()
    {
        // Arrange
        var startDate = new DateOnly(2023, 1, 1);
        var endDate = new DateOnly(2023, 1, 15);
        List<WorkItemProgressStateDto> workItems =
        [
            new() {
                Id = Guid.NewGuid(),
                LevelOrder = 1,
                Tier = WorkTypeTier.Portfolio,
                StatusCategory = StatusCategory.Done,
                ParentId = null,
                Created = Instant.FromUtc(2023, 1, 2, 12, 15),
                DoneTimestamp = Instant.FromUtc(2023, 1, 7, 12, 15)
            }
        ];

        List<WorkItemProgressDailyRollupDto> expectedData =
        [
            WorkItemProgressDailyRollupDto.CreateEmpty(new DateOnly(2023, 1, 1)),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 2), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 3), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 4), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 5), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 6), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 7), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 8), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 9), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 10), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 11), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 12), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 13), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 14), 0, 0, 0),
            WorkItemProgressDailyRollupDto.Create(new DateOnly(2023, 1, 15), 0, 0, 0)
        ];

        // Act
        var result = WorkItemProgressDailyRollupDto.CreateList(startDate, endDate, workItems);

        // Assert
        Assert.Equal(expectedData.Count, result.Count);
        for (var i = 0; i < expectedData.Count; i++)
        {
            Assert.Equal(expectedData[i].Date, result[i].Date);
            Assert.Equal(expectedData[i].Proposed, result[i].Proposed);
            Assert.Equal(expectedData[i].Active, result[i].Active);
            Assert.Equal(expectedData[i].Done, result[i].Done);
            Assert.Equal(expectedData[i].Total, result[i].Total);
        }
    }
}
