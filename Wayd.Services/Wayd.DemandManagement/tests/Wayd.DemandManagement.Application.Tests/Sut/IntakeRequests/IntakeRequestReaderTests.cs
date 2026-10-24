using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests;

/// <summary>
/// The read rules, one reader and one request at a time: who sees a request's summary, its substance, and
/// who may review it.
/// </summary>
public sealed class IntakeRequestReaderTests
{
    private readonly IntakeDesk _open = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.Open).Generate();
    private readonly IntakeDesk _summaryOnly = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.SummaryOnly).Generate();
    private readonly IntakeDesk _private = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.Private).Generate();

    private static IntakeRequestReader Viewer(Guid? employeeId = null) => new(employeeId ?? Guid.NewGuid(), false, false, []);

    private static IntakeRequestReader Reviewer(Guid deskId, bool holdsReview = true) =>
        new(Guid.NewGuid(), holdsReview, false, [deskId]);

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
    public void TheRequester_OnAPrivateDesk_SeesEverything()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = new IntakeRequestFaker().WithDeskId(_private.Id).WithRequesterId(requesterId).Generate();

        // Act
        var sees = Sees(Viewer(requesterId), request);

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void TheSubmitter_OnAPrivateDesk_SeesEverything()
    {
        // Arrange
        var submittedById = Guid.NewGuid();
        var request = new IntakeRequestFaker().WithDeskId(_private.Id).WithSubmittedById(submittedById).Generate();

        // Act
        var sees = Sees(Viewer(submittedById), request);

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void AParticipant_OnASummaryOnlyDesk_SeesTheSubstance()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var request = new IntakeRequestFaker().WithDeskId(_summaryOnly.Id).WithParticipants(participantId).Generate();

        // Act
        var sees = Sees(Viewer(participantId), request);

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void AReviewerOfAnotherDesk_OnASummaryOnlyDesk_SeesTheSubstance()
    {
        // Act — review on any desk grants substance on every desk the reader can see
        var sees = Sees(Reviewer(_open.Id), OnDesk(_summaryOnly));

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void AReviewerOfAnotherDesk_OnAPrivateDesk_SeesNothing()
    {
        // Act — a private desk withholds even the summary from reviewers of other desks
        var sees = Sees(Reviewer(_open.Id), OnDesk(_private));

        // Assert
        sees.Should().Be((false, false));
    }

    [Fact]
    public void AReviewerOfThePrivateDesk_SeesEverything()
    {
        // Act
        var sees = Sees(Reviewer(_private.Id), OnDesk(_private));

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void ANamedReviewerWithoutThePermission_OnThePrivateDesk_SeesNothing()
    {
        // Act — membership without Review is not review
        var sees = Sees(Reviewer(_private.Id, holdsReview: false), OnDesk(_private));

        // Assert
        sees.Should().Be((false, false));
    }

    [Fact]
    public void ReviewWithAdminister_OnAPrivateDesk_SeesEverything()
    {
        // Act
        var sees = Sees(new IntakeRequestReader(Guid.NewGuid(), true, true, []), OnDesk(_private));

        // Assert
        sees.Should().Be((true, true));
    }

    [Fact]
    public void AdministerWithoutReview_OnAPrivateDesk_SeesNothing()
    {
        // Act — Administer waives the membership, never the permission
        var sees = Sees(new IntakeRequestReader(Guid.NewGuid(), false, true, []), OnDesk(_private));

        // Assert
        sees.Should().Be((false, false));
    }

    [Fact]
    public void AReaderWithNoEmployee_IsNoRequesterOfAnything()
    {
        // Arrange
        var reader = new IntakeRequestReader(null, false, false, []);

        // Act
        var sees = Sees(reader, OnDesk(_private));

        // Assert
        sees.Should().Be((false, false));
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    public void CanReview_IsThePermissionPlusMembership_WaivedByAdminister(
        bool holdsReview, bool holdsAdminister, bool isMember, bool expected)
    {
        // Arrange
        var deskId = Guid.NewGuid();
        var reader = new IntakeRequestReader(Guid.NewGuid(), holdsReview, holdsAdminister, isMember ? [deskId] : []);

        // Act
        var result = reader.CanReview(deskId);

        // Assert
        result.Should().Be(expected);
    }
}
