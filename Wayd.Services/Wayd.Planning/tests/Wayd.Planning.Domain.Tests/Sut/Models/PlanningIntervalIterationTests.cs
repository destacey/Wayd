using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Domain.Tests.Sut.Models;

public class PlanningIntervalIterationTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly PlanningIntervalIterationFaker _faker = new();

    public PlanningIntervalIterationTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
    }

    #region Constructor

    [Fact]
    public void Constructor_WhenValid_ReturnsPlanningIntervalIteration()
    {
        // Arrange
        var faker = _faker.Generate();

        // Act
        var result = new PlanningIntervalIteration(faker.PlanningIntervalId, faker.Name, faker.Category, faker.DateRange);

        // Assert
        result.Should().NotBeNull();
        result.PlanningIntervalId.Should().Be(faker.PlanningIntervalId);
        result.Name.Should().Be(faker.Name);
        result.Category.Should().Be(faker.Category);
        result.DateRange.Should().Be(faker.DateRange);
    }

    #endregion Constructor

    #region Private Setters

    [Fact]
    public void Constructor_WhenValid_NameIsTrimmed()
    {
        // Arrange
        var faker = _faker.Generate();

        // Act
        var sut = new PlanningIntervalIteration(faker.PlanningIntervalId, $"  {faker.Name} ", faker.Category, faker.DateRange);

        // Assert
        sut.Should().NotBeNull();
        sut.Name.Should().Be(faker.Name);
    }

    [Fact]
    public void Name_IsValidWithWhitespace_ThrowsArgumentException()
    {
        // Arrange
        var name = null as string;
        var faker = _faker.Generate();

        // Act
        Action act = () => new PlanningIntervalIteration(faker.PlanningIntervalId, name!, faker.Category, faker.DateRange);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithMessage("Value cannot be null. (Parameter 'Name')");
    }

    [Fact]
    public void Name_IsNull_ThrowsArgumentException()
    {
        // Arrange
        var name = null as string;
        var faker = _faker.Generate();

        // Act
        Action act = () => new PlanningIntervalIteration(faker.PlanningIntervalId, name!, faker.Category, faker.DateRange);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithMessage("Value cannot be null. (Parameter 'Name')");
    }

    [Fact]
    public void Name_IsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var name = string.Empty;
        var faker = _faker.Generate();

        // Act
        Action act = () => new PlanningIntervalIteration(faker.PlanningIntervalId, name, faker.Category, faker.DateRange);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    [Fact]
    public void Name_IsWhiteSpace_ThrowsArgumentException()
    {
        // Arrange
        var name = " ";
        var faker = _faker.Generate();

        // Act
        Action act = () => new PlanningIntervalIteration(faker.PlanningIntervalId, name, faker.Category, faker.DateRange);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    [Fact]
    public void DateRange_IsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var dateRange = null as LocalDateRange;
        var faker = _faker.Generate();

        // Act
        Action act = () => new PlanningIntervalIteration(faker.PlanningIntervalId, faker.Name, faker.Category, dateRange!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithMessage("Value cannot be null. (Parameter 'DateRange')");
    }

    #endregion Private Setters

    #region Create

    [Fact]
    public void Create_WhenValid_ReturnsPlanningIntervalIteration()
    {
        // Arrange
        var faker = _faker.Generate();

        // Act
        var result = PlanningIntervalIteration.Create(faker.PlanningIntervalId, faker.Name, faker.Category, faker.DateRange);

        // Assert
        result.Should().NotBeNull();
        result.PlanningIntervalId.Should().Be(faker.PlanningIntervalId);
        result.Name.Should().Be(faker.Name);
        result.Category.Should().Be(faker.Category);
        result.DateRange.Should().Be(faker.DateRange);
    }

    #endregion Create

    #region Update

    [Fact]
    public void Update_WhenValid_ReturnsSuccess()
    {
        // Arrange
        var faker = _faker.Generate();
        var sut = PlanningIntervalIteration.Create(faker.PlanningIntervalId, faker.Name, IterationCategory.Development, faker.DateRange);
        var expectedName = faker.Name + "Updated";
        var expectedCategory = IterationCategory.InnovationAndPlanning;
        var expectedDateRange = new LocalDateRange(faker.DateRange.Start.Plus(Period.FromWeeks(1)), faker.DateRange.End.Plus(Period.FromWeeks(1)));

        // Act
        var result = sut.Update(expectedName, expectedCategory, expectedDateRange);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be(expectedName);
        sut.Category.Should().Be(expectedCategory);
        sut.DateRange.Should().Be(expectedDateRange);
    }

    #endregion Update

    #region StateOn

    [Fact]
    public void StateOn_ShouldReturnCompleted_WhenDateIsPast()
    {
        // Arrange
        var today = _dateTimeProvider.Today;
        var iterationDateRange = new LocalDateRange(today.Plus(Period.FromWeeks(-4)), today.Plus(Period.FromWeeks(-2)));
        var sut = _faker.WithDateRange(iterationDateRange).Generate();

        // Act
        var result = sut.StateOn(today);

        // Assert
        result.Should().Be(IterationState.Completed);
    }

    [Fact]
    public void StateOn_ShouldReturnActive_WhenDateIsWithinRange()
    {
        // Arrange
        var today = _dateTimeProvider.Today;
        var iterationDateRange = new LocalDateRange(today.Plus(Period.FromWeeks(-1)), today.Plus(Period.FromWeeks(1)));
        var sut = _faker.WithDateRange(iterationDateRange).Generate();

        // Act
        var result = sut.StateOn(today);

        // Assert
        result.Should().Be(IterationState.Active);
    }

    [Fact]
    public void StateOn_ShouldReturnFuture_WhenDateIsFuture()
    {
        // Arrange
        var today = _dateTimeProvider.Today;
        var iterationDateRange = new LocalDateRange(today.Plus(Period.FromWeeks(1)), today.Plus(Period.FromWeeks(3)));
        var sut = _faker.WithDateRange(iterationDateRange).Generate();

        // Act
        var result = sut.StateOn(today);

        // Assert
        result.Should().Be(IterationState.Future);
    }

    #endregion StateOn

}