using FluentAssertions;
using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Common.Application.Tests.Sut.BackgroundJobs;

public sealed class GetBackgroundJobTypesQueryHandlerTests
{
    private static GetBackgroundJobTypesQueryHandler CreateHandler() => new();

    [Fact]
    public async Task Handle_ReturnsEveryJobType()
    {
        // Arrange
        var handler = CreateHandler();

        // Act — the run menu offers every type, so none may be filtered out here.
        var result = await handler.Handle(new GetBackgroundJobTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(Enum.GetValues<BackgroundJobType>().Length);
    }

    [Fact]
    public async Task Handle_MarksSchedulableTypesFromTheSharedSet()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetBackgroundJobTypesQuery(), TestContext.Current.CancellationToken);

        // Assert — the flag the recurring-job form filters on must agree with the set the API gates
        // on, or the form offers types the API rejects.
        foreach (var dto in result)
        {
            dto.IsSchedulable.Should().Be(SchedulableBackgroundJobTypes.Contains((BackgroundJobType)dto.Id));
        }
    }

    [Fact]
    public async Task Handle_ForRunOnDemandOnlyType_IsNotSchedulable()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetBackgroundJobTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single(t => t.Id == (int)BackgroundJobType.IterationsSync)
            .IsSchedulable.Should().BeFalse();
    }
}
