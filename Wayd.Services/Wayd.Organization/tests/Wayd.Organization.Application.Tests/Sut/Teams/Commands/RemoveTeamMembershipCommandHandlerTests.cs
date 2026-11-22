using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Identity;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public sealed class RemoveTeamMembershipCommandHandlerTests : IDisposable
{
    private static readonly TestingDateTimeProvider Clock = new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly RemoveTeamMembershipCommandHandler _handler;

    public RemoveTeamMembershipCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);
        _handler = new RemoveTeamMembershipCommandHandler(_dbContext, Clock, currentUser.Object, NullLogger<RemoveTeamMembershipCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTeamDoesNotExist()
    {
        // Arrange

        // Act
        var result = await _handler.Handle(new RemoveTeamMembershipCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Team not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
