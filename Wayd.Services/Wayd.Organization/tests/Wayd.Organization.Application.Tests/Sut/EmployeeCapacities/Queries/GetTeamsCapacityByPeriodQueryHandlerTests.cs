using Moq;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities.Queries;

public class GetTeamsCapacityByPeriodQueryHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 13, 9, 30);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<IFeatureFlags> _featureFlags = new();

    [Fact]
    public async Task Handle_WithTheFeatureOff_ReturnsNullWhateverIsAllocated()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        capacity.ChangeAllocations([new AllocationChange(teamId, null, new LocalDate(2027, 1, 1), null, 100)], EventActor.System, Now).IsSuccess.Should().BeTrue();
        _dbContext.AddEmployeeCapacity(capacity);
        _featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.TeamCapacityManagement, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var handler = new GetTeamsCapacityByPeriodQueryHandler(_dbContext, new Mock<ISettings<SchedulingSettings>>().Object, _featureFlags.Object);

        // Act
        var result = await handler.Handle(
            new GetTeamsCapacityByPeriodQuery([teamId], [new CapacityPeriod(new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 12))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
