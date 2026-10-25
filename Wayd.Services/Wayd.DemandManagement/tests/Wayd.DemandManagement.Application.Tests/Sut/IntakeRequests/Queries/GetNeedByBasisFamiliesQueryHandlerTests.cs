using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetNeedByBasisFamiliesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEachFamilyWithItsName()
    {
        // Arrange
        var handler = new GetNeedByBasisFamiliesQueryHandler();

        // Act
        var result = await handler.Handle(new GetNeedByBasisFamiliesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(f => (f.Code, f.Name)).Should().Equal(
            (NeedByBasisFamily.FixedForUs, "Fixed for us"),
            (NeedByBasisFamily.ChosenByUs, "We chose it"));
    }
}
