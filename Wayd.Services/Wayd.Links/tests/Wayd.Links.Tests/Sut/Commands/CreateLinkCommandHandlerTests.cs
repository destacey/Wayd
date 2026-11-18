using Wayd.Common.Domain.Links;
using Wayd.Links.Commands;
using Wayd.Links.Tests.Infrastructure;

namespace Wayd.Links.Tests.Sut.Commands;

public sealed class CreateLinkCommandHandlerTests
{
    private readonly LinkTestHarness _harness = new();

    private CreateLinkCommandHandler CreateHandler() =>
        new(_harness.Resolver(), _harness.Writer(), _harness.DbContext);

    private CreateLinkCommand Command(string url = "https://example.com/charter") =>
        new(LinkTestHarness.Gadget.Key, _harness.OwnerId, "Charter", url);

    [Fact]
    public async Task Handle_ShouldAddTheLinkAndSaveOnce()
    {
        // Arrange — the caller may manage the record

        // Act
        var result = await CreateHandler().Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var link = _harness.DbContext.Links.Should().ContainSingle().Subject;
        link.Id.Should().Be(result.Value);
        link.DomainEvents.Should().ContainSingle().Which.Actor.EmployeeId.Should().Be(_harness.EmployeeId);
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheEmployeeTheAccountIsLinkedToNow_NotTheOneItsTokenCarries()
    {
        // Arrange — the token was issued before the account was linked to an employee
        _harness.CurrentUser.Setup(u => u.GetEmployeeId()).Returns((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var actor = _harness.DbContext.Links.Single().DomainEvents.Single().Actor;
        actor.EmployeeId.Should().Be(_harness.EmployeeId);
        actor.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WithoutManagingTheRecord()
    {
        // Arrange
        _harness.Owner(canManage: false);

        // Act
        var result = await CreateHandler().Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.CannotManage);
        _harness.DbContext.Links.Should().BeEmpty();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeNotFound_WhenTheRecordCannotBeRead()
    {
        // Arrange
        _harness.OwnerHidden();

        // Act
        var result = await CreateHandler().Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.NotFound);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_ForAReadOnlyToken()
    {
        // Arrange
        _harness.CurrentPrincipal.SetupGet(p => p.IsReadOnly).Returns(true);

        // Act
        var result = await CreateHandler().Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.CannotManage);
    }

    [Fact]
    public async Task Handle_ShouldNotSave_WhenTheWriterRefusesTheLink()
    {
        // Arrange — an address the validator would have refused, sent past it
        var command = Command("example.com/charter");

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkErrors.UrlNotWebAddress);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("javascript:alert(1)")]
    public void Validator_ShouldRefuseAUrlThatIsNotAWebAddress(string url)
    {
        // Arrange
        var validator = new CreateLinkCommandValidator();

        // Act
        var result = validator.Validate(Command(url));

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(LinkErrors.UrlNotWebAddress);
    }
}
