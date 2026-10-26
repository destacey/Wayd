using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests;

/// <summary>
/// The read rules, one reader and one request at a time: who sees a request's summary, its substance, and
/// who acts on its desk.
/// </summary>
public sealed class IntakeRequestReaderTests
{
    private readonly IntakeDesk _open = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.Open).Generate();
    private readonly IntakeDesk _summaryOnly = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.SummaryOnly).Generate();
    private readonly IntakeDesk _private = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.Private).Generate();

    private static IntakeRequestReader Viewer(Guid? employeeId = null) =>
        new(employeeId ?? Guid.NewGuid(), HoldsView: true, IsDemandManagementAdmin: false, new Dictionary<Guid, IntakeDeskRole>());

    private static IntakeRequestReader NoClaims(Guid? employeeId = null) =>
        new(employeeId ?? Guid.NewGuid(), HoldsView: false, IsDemandManagementAdmin: false, new Dictionary<Guid, IntakeDeskRole>());

    private static IntakeRequestReader Member(Guid deskId, IntakeDeskRole role = IntakeDeskRole.Agent, bool holdsView = false) =>
        new(Guid.NewGuid(), holdsView, IsDemandManagementAdmin: false, new Dictionary<Guid, IntakeDeskRole> { [deskId] = role });

    private static IntakeRequestReader DemandManagementAdmin() =>
        new(Guid.NewGuid(), HoldsView: false, IsDemandManagementAdmin: true, new Dictionary<Guid, IntakeDeskRole>());

    private (bool Summary, bool Substance) Sees(IntakeRequestReader reader, IntakeRequest request)
    {
        var desks = new[] { _open, _summaryOnly, _private }.AsQueryable();
        var requests = new[] { request }.AsQueryable();

        var summary = requests.Where(reader.CanSeeSummary(desks)).Any();
        var substance = summary && requests.Where(reader.CanSeeSubstance(desks)).Any();

        return (summary, substance);
    }

    private static IntakeRequest OnDesk(IntakeDesk desk) =>
        new IntakeRequestFaker().WithDeskId(desk.Id).Generate();

    #region Summary and substance

    [Fact]
    public void AViewer_OnAnOpenDesk_SeesTheSummaryAndTheSubstance()
    {
        // Act
        var sees = Sees(Viewer(), OnDesk(_open));

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void AViewer_OnASummaryOnlyDesk_SeesTheSummaryOnly()
    {
        // Act
        var sees = Sees(Viewer(), OnDesk(_summaryOnly));

        // Assert
        sees.Should().Be((true, false));
    }

    [Fact]
    public void AViewer_OnAPrivateDesk_SeesNothing()
    {
        // Act
        var sees = Sees(Viewer(), OnDesk(_private));

        // Assert
        sees.Should().Be((false, false));
    }

    [Fact]
    public void SomeoneWithoutView_OnAnOpenDesk_SeesNothing()
    {
        // Act — browsing other people's requests is what View grants
        var sees = Sees(NoClaims(), OnDesk(_open));

        // Assert
        sees.Should().Be((false, false));
    }

    [Fact]
    public void TheRequesterWithoutView_OnAPrivateDesk_SeesEverything()
    {
        // Arrange — the request's own people need no claim
        var requesterId = Guid.NewGuid();
        var request = new IntakeRequestFaker().WithDeskId(_private.Id).WithRequesterId(requesterId).Generate();

        // Act
        var sees = Sees(NoClaims(requesterId), request);

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void TheSubmitterWithoutView_OnAPrivateDesk_SeesEverything()
    {
        // Arrange
        var submittedById = Guid.NewGuid();
        var request = new IntakeRequestFaker().WithDeskId(_private.Id).WithSubmittedById(submittedById).Generate();

        // Act
        var sees = Sees(NoClaims(submittedById), request);

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void AParticipantWithoutView_OnASummaryOnlyDesk_SeesTheSubstance()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var request = new IntakeRequestFaker().WithDeskId(_summaryOnly.Id).WithParticipants(participantId).Generate();

        // Act
        var sees = Sees(NoClaims(participantId), request);

        // Assert
        sees.Should().Be((true, true));
    }

    [Theory]
    [InlineData(IntakeDeskRole.Agent)]
    [InlineData(IntakeDeskRole.Admin)]
    public void AMemberWithoutView_OfThePrivateDesk_SeesEverything(IntakeDeskRole role)
    {
        // Act — membership is the grant
        var sees = Sees(Member(_private.Id, role), OnDesk(_private));

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void AnAgentOfAnotherDesk_OnASummaryOnlyDesk_SeesTheSummaryOnly()
    {
        // Act — working one desk grants nothing on another
        var sees = Sees(Member(_open.Id, holdsView: true), OnDesk(_summaryOnly));

        // Assert
        sees.Should().Be((true, false));
    }

    [Fact]
    public void AnAgentOfAnotherDesk_OnAPrivateDesk_SeesNothing()
    {
        // Act
        var sees = Sees(Member(_open.Id, holdsView: true), OnDesk(_private));

        // Assert
        sees.Should().Be((false, false));
    }

    [Fact]
    public void TheDemandManagementAdmin_OnAPrivateDesk_SeesEverything()
    {
        // Act
        var sees = Sees(DemandManagementAdmin(), OnDesk(_private));

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void AReaderWithNoEmployee_IsNoRequesterOfAnything()
    {
        // Arrange
        var reader = new IntakeRequestReader(null, true, false, new Dictionary<Guid, IntakeDeskRole>());

        // Act
        var sees = Sees(reader, OnDesk(_private));

        // Assert
        sees.Should().Be((false, false));
    }

    #endregion Summary and substance

    #region Desk roles

    [Theory]
    [InlineData(IntakeDeskRole.Agent, true, false)]
    [InlineData(IntakeDeskRole.Admin, true, true)]
    public void AMember_ActsOnTheirDeskByRole(IntakeDeskRole role, bool expectedAgent, bool expectedDeskAdmin)
    {
        // Arrange
        var deskId = Guid.NewGuid();
        var reader = Member(deskId, role);

        // Act
        var agent = reader.IsAgentOn(deskId);
        var deskAdmin = reader.IsDeskAdminOn(deskId);

        // Assert
        agent.Should().Be(expectedAgent);
        deskAdmin.Should().Be(expectedDeskAdmin);
    }

    [Fact]
    public void AMember_DoesNotActOnAnotherDesk()
    {
        // Arrange
        var reader = Member(Guid.NewGuid(), IntakeDeskRole.Admin);
        var otherDeskId = Guid.NewGuid();

        // Act
        var agent = reader.IsAgentOn(otherDeskId);
        var deskAdmin = reader.IsDeskAdminOn(otherDeskId);

        // Assert
        agent.Should().BeFalse();
        deskAdmin.Should().BeFalse();
    }

    [Fact]
    public void TheDemandManagementAdmin_IsDeskAdminOnEveryDesk()
    {
        // Arrange
        var reader = DemandManagementAdmin();
        var deskId = Guid.NewGuid();

        // Act
        var agent = reader.IsAgentOn(deskId);
        var deskAdmin = reader.IsDeskAdminOn(deskId);

        // Assert
        agent.Should().BeTrue();
        deskAdmin.Should().BeTrue();
    }

    [Fact]
    public void AViewer_ActsOnNoDesk()
    {
        // Arrange
        var reader = Viewer();

        // Act
        var agent = reader.IsAgentOn(_open.Id);

        // Assert
        agent.Should().BeFalse();
    }

    #endregion Desk roles
}
