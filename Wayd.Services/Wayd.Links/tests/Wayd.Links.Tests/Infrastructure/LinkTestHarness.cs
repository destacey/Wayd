using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Links;
using Wayd.Tests.Shared;

namespace Wayd.Links.Tests.Infrastructure;

/// <summary>
/// The pieces every link test wires the same way: a fake context, an owner type the test decides the answer of, and a
/// signed-in caller.
/// </summary>
public sealed class LinkTestHarness
{
    /// <summary>The owner type every link in these tests is on.</summary>
    public static readonly LinkOwnerTypeDescriptor Gadget = new("test.link-handlers.gadget", "Gadget", "Gadget");

    public LinkTestHarness()
    {
        LinkOwnerTypes.Register(Gadget);
        OwnerType.Setup(t => t.OwnerType).Returns(Gadget.Key);
        CurrentUser.Setup(u => u.GetUserId()).Returns("user-1");
        CurrentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
        Owner(canManage: true);
    }

    public FakeLinksDbContext DbContext { get; } = new();

    public Mock<ILinkOwnerType> OwnerType { get; } = new();

    public Mock<ICurrentPrincipal> CurrentPrincipal { get; } = new();

    public Mock<ICurrentUser> CurrentUser { get; } = new();

    public TestingDateTimeProvider DateTimeProvider { get; } = new(new FakeClock(Instant.FromUtc(2026, 11, 17, 9, 30)));

    /// <summary>The caller's linked employee.</summary>
    public Guid EmployeeId { get; } = Guid.NewGuid();

    /// <summary>The record every test's links are on.</summary>
    public Guid OwnerId { get; } = Guid.NewGuid();

    /// <summary>The caller, as link events record them.</summary>
    public EventActor Actor => EventActor.User("user-1", EmployeeId);

    /// <summary>Makes the owner type find the record and answer whether the caller may manage its links.</summary>
    public void Owner(bool canManage) =>
        OwnerType.Setup(t => t.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id == OwnerId ? new LinkOwnerAccess(canManage) : null);

    /// <summary>Makes the owner type say the record does not exist or cannot be read.</summary>
    public void OwnerHidden() =>
        OwnerType.Setup(t => t.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((LinkOwnerAccess?)null);

    /// <summary>A link on the given record, or the test's own, added to the context with its creation event cleared.</summary>
    public Link AddLink(string name = "Charter", string url = "https://example.com/charter", Guid? ownerId = null)
    {
        var link = Link.Create(Gadget.Key, ownerId ?? OwnerId, name, url, EventActor.System, DateTimeProvider.Now).Value;
        link.ClearDomainEvents();
        DbContext.AddLinks(link);
        return link;
    }

    public LinkOwnerAccessResolver Resolver() => new([OwnerType.Object], CurrentPrincipal.Object, CurrentUser.Object);

    public LinkWriter Writer() => new(DbContext, [OwnerType.Object], DateTimeProvider);
}
