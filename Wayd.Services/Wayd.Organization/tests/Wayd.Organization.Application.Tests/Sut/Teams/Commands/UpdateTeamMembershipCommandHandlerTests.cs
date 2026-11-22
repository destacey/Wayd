using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Identity;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public sealed class UpdateTeamMembershipCommandHandlerTests : IDisposable
{
    private static readonly TestingDateTimeProvider Clock = new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly UpdateTeamMembershipCommandHandler _handler;

    public UpdateTeamMembershipCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);
        _handler = new UpdateTeamMembershipCommandHandler(_dbContext, Clock, currentUser.Object, NullLogger<UpdateTeamMembershipCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTeamDoesNotExist()
    {
        // Arrange

        // Act
        var result = await _handler.Handle(new UpdateTeamMembershipCommand(Guid.NewGuid(), Guid.NewGuid(), new MembershipDateRange(new LocalDate(2026, 6, 1), null)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Team not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
