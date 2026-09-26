using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;
using NodaTime;

namespace Wayd.Organization.Domain.Tests.Sut.Models;

public class TeamOperatingModelTests
{
    #region Create

    [Fact]
    public void Create_WithValidData_Success()
    {
        // ARRANGE
        var startDate = new LocalDate(2024, 1, 1);
        var methodology = Methodology.Scrum;
        var sizingMethod = SizingMethod.StoryPoints;

        // ACT
        var result = TeamOperatingModel.Create(startDate, methodology, sizingMethod, "UTC", 1);

        // ASSERT
        result.IsSuccess.Should().BeTrue();
        result.Value.DateRange.Start.Should().Be(startDate);
        result.Value.DateRange.End.Should().BeNull();
        result.Value.Methodology.Should().Be(methodology);
        result.Value.SizingMethod.Should().Be(sizingMethod);
        result.Value.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void Create_WithCurrentModel_ClosesCurrentModelAndCreatesNew()
    {
        // ARRANGE
        var currentStartDate = new LocalDate(2023, 1, 1);
        var newStartDate = new LocalDate(2024, 1, 1);
        var methodology = Methodology.Kanban;
        var sizingMethod = SizingMethod.Count;

        var currentModel = new TeamOperatingModelFaker(currentStartDate)
            .Generate();

        // ACT
        var result = TeamOperatingModel.Create(newStartDate, methodology, sizingMethod, "UTC", 1, currentModel);

        // ASSERT
        result.IsSuccess.Should().BeTrue();
        result.Value.DateRange.Start.Should().Be(newStartDate);
        result.Value.DateRange.End.Should().BeNull();
        result.Value.IsCurrent.Should().BeTrue();

        // Current model should be closed
        currentModel.IsCurrent.Should().BeFalse();
        currentModel.DateRange.End.Should().Be(new LocalDate(2023, 12, 31));
    }

    [Fact]
    public void Create_WithNewStartDateBeforeCurrentStart_ReturnsFailure()
    {
        // ARRANGE
        var currentStartDate = new LocalDate(2024, 1, 1);
        var newStartDate = new LocalDate(2023, 12, 31);
        var methodology = Methodology.Scrum;
        var sizingMethod = SizingMethod.StoryPoints;

        var currentModel = new TeamOperatingModelFaker(currentStartDate)
            .Generate();

        // ACT
        var result = TeamOperatingModel.Create(newStartDate, methodology, sizingMethod, "UTC", 1, currentModel);

        // ASSERT
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("New operating model start date must be after the current model's start date.");
    }

    [Fact]
    public void Create_WithNewStartDateEqualToCurrentStart_ReturnsFailure()
    {
        // ARRANGE
        var currentStartDate = new LocalDate(2024, 1, 1);
        var newStartDate = new LocalDate(2024, 1, 1);
        var methodology = Methodology.Scrum;
        var sizingMethod = SizingMethod.StoryPoints;

        var currentModel = new TeamOperatingModelFaker(currentStartDate)
            .Generate();

        // ACT
        var result = TeamOperatingModel.Create(newStartDate, methodology, sizingMethod, "UTC", 1, currentModel);

        // ASSERT
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("New operating model start date must be after the current model's start date.");
    }

    [Fact]
    public void Create_WithClosedCurrentModel_DoesNotCloseCurrent()
    {
        // ARRANGE
        var currentStartDate = new LocalDate(2023, 1, 1);
        var currentEndDate = new LocalDate(2023, 12, 31);
        var newStartDate = new LocalDate(2024, 1, 1);
        var methodology = Methodology.Scrum;
        var sizingMethod = SizingMethod.StoryPoints;

        var currentModel = new TeamOperatingModelFaker()
            .WithDateRange(currentStartDate, currentEndDate)
            .Generate();

        // ACT
        var result = TeamOperatingModel.Create(newStartDate, methodology, sizingMethod, "UTC", 1, currentModel);

        // ASSERT
        result.IsSuccess.Should().BeTrue();

        // Closed model should remain unchanged
        currentModel.DateRange.End.Should().Be(currentEndDate);
    }

    [Fact]
    public void Create_StoresTimeZoneAndCommitmentGraceDays()
    {
        // ARRANGE
        var startDate = new LocalDate(2024, 1, 1);

        // ACT
        var result = TeamOperatingModel.Create(startDate, Methodology.Scrum, SizingMethod.StoryPoints, "America/Chicago", 2);

        // ASSERT
        result.IsSuccess.Should().BeTrue();
        result.Value.TimeZone.Should().Be("America/Chicago");
        result.Value.CommitmentGraceDays.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not/AZone")]
    [InlineData("Eastern Standard Time")]
    public void Create_WithUnknownTimeZone_ReturnsFailure(string timeZone)
    {
        // ARRANGE
        var startDate = new LocalDate(2024, 1, 1);

        // ACT
        var result = TeamOperatingModel.Create(startDate, Methodology.Scrum, SizingMethod.StoryPoints, timeZone, 1);

        // ASSERT
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a valid IANA time zone");
    }

    [Fact]
    public void Create_WithNegativeCommitmentGraceDays_ReturnsFailure()
    {
        // ARRANGE
        var startDate = new LocalDate(2024, 1, 1);

        // ACT
        var result = TeamOperatingModel.Create(startDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", -1);

        // ASSERT
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The commitment grace period cannot be negative.");
    }

    [Fact]
    public void Create_WithInvalidSchedule_LeavesCurrentModelOpen()
    {
        // ARRANGE
        var currentModel = new TeamOperatingModelFaker(new LocalDate(2023, 1, 1)).Generate();

        // ACT
        var result = TeamOperatingModel.Create(new LocalDate(2024, 1, 1), Methodology.Scrum, SizingMethod.StoryPoints, "Not/AZone", 1, currentModel);

        // ASSERT
        result.IsFailure.Should().BeTrue();
        currentModel.IsCurrent.Should().BeTrue();
    }

    #endregion Create

    #region Update

    [Fact]
    public void Update_WithValidData_Success()
    {
        // ARRANGE
        var model = new TeamOperatingModelFaker()
            .WithMethodology(Methodology.Scrum)
            .WithSizingMethod(SizingMethod.StoryPoints)
            .Generate();

        var newMethodology = Methodology.Kanban;
        var newSizingMethod = SizingMethod.Count;

        // ACT
        var result = model.Update(newMethodology, newSizingMethod, "UTC", 1);

        // ASSERT
        result.IsSuccess.Should().BeTrue();
        model.Methodology.Should().Be(newMethodology);
        model.SizingMethod.Should().Be(newSizingMethod);
    }

    [Fact]
    public void Update_WithSameValues_Success()
    {
        // ARRANGE
        var methodology = Methodology.Scrum;
        var sizingMethod = SizingMethod.StoryPoints;

        var model = new TeamOperatingModelFaker()
            .WithMethodology(methodology)
            .WithSizingMethod(sizingMethod)
            .Generate();

        // ACT
        var result = model.Update(methodology, sizingMethod, "UTC", 1);

        // ASSERT
        result.IsSuccess.Should().BeTrue();
        model.Methodology.Should().Be(methodology);
        model.SizingMethod.Should().Be(sizingMethod);
    }

    [Fact]
    public void Update_CorrectsTimeZoneAndCommitmentGraceDays()
    {
        // ARRANGE
        var model = new TeamOperatingModelFaker()
            .WithTimeZone("America/New_York")
            .WithCommitmentGraceDays(1)
            .Generate();

        // ACT
        var result = model.Update(model.Methodology, model.SizingMethod, "America/Chicago", 0);

        // ASSERT
        result.IsSuccess.Should().BeTrue();
        model.TimeZone.Should().Be("America/Chicago");
        model.CommitmentGraceDays.Should().Be(0);
    }

    [Fact]
    public void Update_WithUnknownTimeZone_ReturnsFailureAndChangesNothing()
    {
        // ARRANGE
        var model = new TeamOperatingModelFaker()
            .WithMethodology(Methodology.Scrum)
            .WithTimeZone("America/New_York")
            .Generate();

        // ACT
        var result = model.Update(Methodology.Kanban, model.SizingMethod, "Not/AZone", 1);

        // ASSERT
        result.IsFailure.Should().BeTrue();
        model.Methodology.Should().Be(Methodology.Scrum);
        model.TimeZone.Should().Be("America/New_York");
    }

    [Fact]
    public void Update_WithNegativeCommitmentGraceDays_ReturnsFailure()
    {
        // ARRANGE
        var model = new TeamOperatingModelFaker().WithCommitmentGraceDays(1).Generate();

        // ACT
        var result = model.Update(model.Methodology, model.SizingMethod, "UTC", -1);

        // ASSERT
        result.IsFailure.Should().BeTrue();
        model.CommitmentGraceDays.Should().Be(1);
    }

    #endregion Update

    #region Close

    [Fact]
    public void Close_WithValidEndDate_Success()
    {
        // ARRANGE
        var startDate = new LocalDate(2024, 1, 1);
        var endDate = new LocalDate(2024, 12, 31);

        var model = new TeamOperatingModelFaker(startDate)
            .Generate();

        // ACT
        model.Close(endDate);

        // ASSERT
        model.DateRange.End.Should().Be(endDate);
        model.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public void Close_WithEndDateEqualToStart_Success()
    {
        // ARRANGE
        var startDate = new LocalDate(2024, 1, 1);
        var endDate = new LocalDate(2024, 1, 1);

        var model = new TeamOperatingModelFaker(startDate)
            .Generate();

        // ACT
        model.Close(endDate);

        // ASSERT
        model.DateRange.End.Should().Be(endDate);
        model.IsCurrent.Should().BeFalse();
    }

    #endregion Close

    #region IsCurrent

    [Fact]
    public void IsCurrent_WithNoEndDate_ReturnsTrue()
    {
        // ARRANGE
        var model = new TeamOperatingModelFaker()
            .AsCurrent()
            .Generate();

        // ACT & ASSERT
        model.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void IsCurrent_WithEndDate_ReturnsFalse()
    {
        // ARRANGE
        var model = new TeamOperatingModelFaker()
            .AsClosed()
            .Generate();

        // ACT & ASSERT
        model.IsCurrent.Should().BeFalse();
    }

    #endregion IsCurrent

    #region Integration Scenarios

    [Fact]
    public void Scenario_CreateMultipleOperatingModelsOverTime_Success()
    {
        // ARRANGE
        var model1StartDate = new LocalDate(2022, 1, 1);
        var result1 = TeamOperatingModel.Create(
            model1StartDate,
            Methodology.Scrum,
            SizingMethod.StoryPoints,
            "UTC",
            1);

        result1.IsSuccess.Should().BeTrue();
        var model1 = result1.Value;

        // Create second operating model (should close first)
        var model2StartDate = new LocalDate(2023, 1, 1);
        var result2 = TeamOperatingModel.Create(
            model2StartDate,
            Methodology.Kanban,
            SizingMethod.Count,
            "UTC",
            1,
            model1);

        result2.IsSuccess.Should().BeTrue();
        var model2 = result2.Value;

        // Create third operating model (should close second)
        var model3StartDate = new LocalDate(2024, 1, 1);
        var result3 = TeamOperatingModel.Create(
            model3StartDate,
            Methodology.Scrum,
            SizingMethod.StoryPoints,
            "UTC",
            1,
            model2);

        result3.IsSuccess.Should().BeTrue();
        var model3 = result3.Value;

        // ASSERT
        // First model should be closed
        model1.IsCurrent.Should().BeFalse();
        model1.DateRange.Start.Should().Be(new LocalDate(2022, 1, 1));
        model1.DateRange.End.Should().Be(new LocalDate(2022, 12, 31));

        // Second model should be closed
        model2.IsCurrent.Should().BeFalse();
        model2.DateRange.Start.Should().Be(new LocalDate(2023, 1, 1));
        model2.DateRange.End.Should().Be(new LocalDate(2023, 12, 31));

        // Third model should be current
        model3.IsCurrent.Should().BeTrue();
        model3.DateRange.Start.Should().Be(new LocalDate(2024, 1, 1));
        model3.DateRange.End.Should().BeNull();
    }

    [Fact]
    public void Scenario_UpdateCurrentOperatingModel_Success()
    {
        // ARRANGE
        var startDate = new LocalDate(2024, 1, 1);

        var result = TeamOperatingModel.Create(
            startDate,
            Methodology.Scrum,
            SizingMethod.StoryPoints,
            "UTC",
            1);

        result.IsSuccess.Should().BeTrue();
        var model = result.Value;

        // ACT - Update the operating model
        var updateResult = model.Update(Methodology.Kanban, SizingMethod.Count, "UTC", 1);

        // ASSERT
        updateResult.IsSuccess.Should().BeTrue();
        model.Methodology.Should().Be(Methodology.Kanban);
        model.SizingMethod.Should().Be(SizingMethod.Count);
        model.IsCurrent.Should().BeTrue();
        model.DateRange.Start.Should().Be(startDate);
        model.DateRange.End.Should().BeNull();
    }

    #endregion Integration Scenarios
}
