using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests;

public sealed class IntakeRequestDiscussionChannelTypeTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IFeatureFlags> _featureFlags = new();
    private readonly FakeDiscussionsDbContext _discussions = new();

    public IntakeRequestDiscussionChannelTypeTests()
    {
        TurnDemandManagement(on: true);
        DemandManagementDiscussionChannels.Register();
    }

    private IntakeRequestDiscussionChannelType CreateSut() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _featureFlags.Object, _discussions);

    /// <summary>Saves the default discussion settings every desk follows.</summary>
    private void UseSettings(DiscussionSettingValues values)
    {
        var intakeRequest = DemandManagementDiscussionChannels.IntakeRequest;
        var saved = DiscussionSettings.Create(intakeRequest, null, intakeRequest.DefaultSettings, values, EventActor.System,
            Instant.FromUtc(2026, 10, 30, 9, 0)).Value;
        if (saved is not null)
            _discussions.AddSettings(saved);
    }

    private void TurnDemandManagement(bool on) =>
        _featureFlags
            .Setup(f => f.IsEnabled(FeatureFlags.Names.DemandManagement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(on);

    private Task<DiscussionAccess?> Resolve(IntakeRequest request) =>
        CreateSut().Resolve(request.Id, TestContext.Current.CancellationToken);

    private Task<IReadOnlySet<Guid>> Mentionable(IntakeRequest request, DiscussionMessageKind kind, params Guid[] ids) =>
        CreateSut().MentionableAmong(request.Id, ids, kind, TestContext.Current.CancellationToken);

    /// <summary>Posts a message on the request as the author, mentioning the given employees, and runs OnPosted.</summary>
    private async Task Post(IntakeRequest request, Guid authorId, DiscussionMessageKind kind, params Guid[] mentionIds)
    {
        var (channel, message) = Message(request, authorId, kind, mentionIds);

        await CreateSut().OnPosted(channel, message, TestContext.Current.CancellationToken);
    }

    /// <summary>Edits a message the author posted earlier to mention the given employees, and runs OnEdited.</summary>
    private async Task EditToMention(IntakeRequest request, Guid authorId, DiscussionMessageKind kind, params Guid[] addedMentionIds)
    {
        var (channel, message) = Message(request, authorId, kind);

        await CreateSut().OnEdited(channel, message, addedMentionIds, TestContext.Current.CancellationToken);
    }

    private (DiscussionChannel Channel, DiscussionMessage Message) Message(IntakeRequest request, Guid authorId,
        DiscussionMessageKind kind, params Guid[] mentionIds)
    {
        var channel = DiscussionChannel.OpenForRecord(DemandManagementDiscussionChannels.IntakeRequest.Key, request.Id,
            EventActor.System, _scenario.Clock.Now).Value;
        var message = DiscussionMessage.Post(channel, kind, authorId, null, "Can you say more?", mentionIds,
            EventActor.System, _scenario.Clock.Now).Value;
        return (channel, message);
    }

    [Fact]
    public void ChannelType_ShouldBeTheRequestsFollowOwnerType()
    {
        // Act
        var channelType = CreateSut().ChannelType;

        // Assert
        // Persisted on every channel, and the key the conversation reads the request's activity under.
        channelType.Should().Be("demand.intake-request");
        channelType.Should().Be(DemandManagementFollowOwners.IntakeRequest.Key);
    }

    [Fact]
    public void Descriptor_ShouldSupportInternalNotesInTheRequestsSection()
    {
        // Act
        var descriptor = DemandManagementDiscussionChannels.IntakeRequest;

        // Assert
        descriptor.Section.Should().Be("Requests");
        descriptor.SupportsInternalNotes.Should().BeTrue();
        descriptor.SupportsSubjects.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ForTheRequester_ReadsAndRepliesButNeverSeesNotes()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), requester);
        _scenario.SignedInAs(requester);

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().Be(new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false, ScopeId: request.DeskId));
    }

    [Fact]
    public async Task Resolve_ForAnAgentOnTheDesk_PostsNotesButDoesNotModerate()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().Be(new DiscussionAccess(CanReadInternal: true, CanPost: true, CanPostInternal: true, CanModerate: false, ScopeId: request.DeskId));
    }

    [Fact]
    public async Task Resolve_ForADeskAdmin_Moderates()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var request = _scenario.Request(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.Employee());
        _scenario.SignedInAs(deskAdmin);

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().Be(new DiscussionAccess(CanReadInternal: true, CanPost: true, CanPostInternal: true, CanModerate: true, ScopeId: request.DeskId));
    }

    [Fact]
    public async Task Resolve_ForTheDemandManagementAdminOffTheDesk_ActsAsDeskAdmin()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().Be(new DiscussionAccess(CanReadInternal: true, CanPost: true, CanPostInternal: true, CanModerate: true, ScopeId: request.DeskId));
    }

    [Fact]
    public async Task Resolve_ForAViewerOnAnOpenDesk_Replies()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().Be(new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false, ScopeId: request.DeskId));
    }

    [Fact]
    public async Task Resolve_ForAViewerOnASummaryOnlyDesk_IsNull()
    {
        // Arrange — replies are substance, so a reader of the summary alone has no conversation
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ForAnOutsiderOnAPrivateDesk_IsNull()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ForARequestThatDoesNotExist_IsNull()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_WhileDemandManagementIsOff_IsNull()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        _scenario.SignedInAs(requester, IntakeScenario.Administer);
        TurnDemandManagement(on: false);

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_WithAReadOnlyToken_ReadsNotesButWritesNothing()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var request = _scenario.Request(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.Employee());
        _scenario.SignedInAs(deskAdmin).WithReadOnlyToken();

        // Act
        var access = await Resolve(request);

        // Assert
        access.Should().Be(new DiscussionAccess(CanReadInternal: true, CanPost: false, CanPostInternal: false, CanModerate: false, ScopeId: request.DeskId));
    }

    [Fact]
    public async Task Resolve_ForAClosedRequest_StillReplies()
    {
        // Arrange — a closed request can still be asked about
        var requester = _scenario.Employee();
        var request = _scenario.RequestIn(_scenario.Desk(), requester, Common.Domain.StatusWorkflows.Enums.StatusCategory.Done);
        _scenario.SignedInAs(requester);

        // Act
        var access = await Resolve(request);

        // Assert
        access!.CanPost.Should().BeTrue();
    }

    [Fact]
    public async Task MentionableAmong_InAnInternalNote_KeepsOnlyTheDesksMembers()
    {
        // Arrange — a mention would tell anyone else that a note exists
        var agent = _scenario.Employee();
        var otherAgent = _scenario.Employee();
        var requester = _scenario.Employee();
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id, otherAgent.Id]), requester, participant.Id);
        _scenario.SignedInAs(agent);

        // Act
        var mentionable = await Mentionable(request, DiscussionMessageKind.InternalNote, otherAgent.Id, requester.Id, participant.Id);

        // Assert
        mentionable.Should().BeEquivalentTo([otherAgent.Id]);
    }

    [Fact]
    public async Task MentionableAmong_InAParticipantsReply_DropsPeopleTheyCouldNotAdd()
    {
        // Arrange — a participant cannot widen the request by mentioning someone
        var agent = _scenario.Employee();
        var requester = _scenario.Employee();
        var participant = _scenario.Employee();
        var outsider = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open, agentIds: [agent.Id]), requester, participant.Id);
        _scenario.SignedInAs(participant);

        // Act
        var mentionable = await Mentionable(request, DiscussionMessageKind.Public, agent.Id, requester.Id, outsider.Id);

        // Assert
        mentionable.Should().BeEquivalentTo([agent.Id, requester.Id]);
    }

    [Fact]
    public async Task MentionableAmong_InTheRequestersReplyOnAnOpenDesk_KeepsSomeoneTheyCouldAdd()
    {
        // Arrange
        var requester = _scenario.Employee();
        var colleague = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(requester);

        // Act
        var mentionable = await Mentionable(request, DiscussionMessageKind.Public, colleague.Id);

        // Assert
        mentionable.Should().BeEquivalentTo([colleague.Id]);
    }

    [Fact]
    public async Task MentionableAmong_InAnAgentsReplyOnAPrivateDesk_DropsOutsiders()
    {
        // Arrange — participation on a private desk is granted, never accrued, so a mention cannot reach an outsider
        var agent = _scenario.Employee();
        var requester = _scenario.Employee();
        var outsider = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: [agent.Id]), requester);
        _scenario.SignedInAs(agent);

        // Act
        var mentionable = await Mentionable(request, DiscussionMessageKind.Public, requester.Id, outsider.Id);

        // Assert
        mentionable.Should().BeEquivalentTo([requester.Id]);
    }

    [Fact]
    public async Task MentionableAmong_AnInactiveEmployee_IsDropped()
    {
        // Arrange
        var agent = _scenario.Employee();
        var leaver = _scenario.Employee(isActive: false);
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee(), leaver.Id);
        _scenario.SignedInAs(agent);

        // Act
        var mentionable = await Mentionable(request, DiscussionMessageKind.Public, leaver.Id);

        // Assert
        mentionable.Should().BeEmpty();
    }

    [Fact]
    public async Task OnPosted_AReplyByAViewerOnAnOpenDesk_MakesThemAParticipant()
    {
        // Arrange
        var viewer = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(viewer, IntakeScenario.View);

        // Act
        await Post(request, viewer.Id, DiscussionMessageKind.Public);

        // Assert
        request.Participants.Should().ContainSingle(p => p.EmployeeId == viewer.Id && p.AddedById == viewer.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0, "the writer's caller saves the participant with the message");
    }

    [Fact]
    public async Task OnPosted_AReplyByAViewerWhoseEmployeeHasLeft_AddsNobody()
    {
        // Arrange — every participant must be an active employee, however they joined
        var leaver = _scenario.Employee(isActive: false);
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(leaver, IntakeScenario.View);

        // Act
        await Post(request, leaver.Id, DiscussionMessageKind.Public);

        // Assert
        request.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task OnPosted_AReplyByAnAgent_AddsNobody()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open, agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        await Post(request, agent.Id, DiscussionMessageKind.Public);

        // Assert
        request.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task OnPosted_AReplyByTheDemandManagementAdminOffTheDesk_AddsNobody()
    {
        // Arrange — the admin works every desk without being a member of it
        var admin = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(admin, IntakeScenario.Administer);

        // Act
        await Post(request, admin.Id, DiscussionMessageKind.Public);

        // Assert
        request.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task OnPosted_AMentionByTheRequesterOnAnOpenDesk_MakesThePersonAParticipant()
    {
        // Arrange
        var requester = _scenario.Employee();
        var colleague = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(requester);

        // Act
        await Post(request, requester.Id, DiscussionMessageKind.Public, colleague.Id);

        // Assert
        request.Participants.Should().ContainSingle(p => p.EmployeeId == colleague.Id && p.AddedById == requester.Id);
    }

    [Fact]
    public async Task MentionableAmong_WhenTheDeskStopsMentionsGrantingAccess_DropsSomeoneNotOnTheRequest()
    {
        // Arrange
        var requester = _scenario.Employee();
        var colleague = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(requester);
        UseSettings(new DiscussionSettingValues(InternalNotesEnabled: true, MentionsGrantAccess: false));

        // Act
        var mentionable = await Mentionable(request, DiscussionMessageKind.Public, colleague.Id, requester.Id);

        // Assert
        mentionable.Should().BeEquivalentTo([requester.Id]);
    }

    [Fact]
    public async Task OnPosted_WhenTheDeskStopsMentionsGrantingAccess_AddsNobodyByMention()
    {
        // Arrange
        var requester = _scenario.Employee();
        var colleague = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(requester);
        UseSettings(new DiscussionSettingValues(InternalNotesEnabled: true, MentionsGrantAccess: false));

        // Act
        await Post(request, requester.Id, DiscussionMessageKind.Public, colleague.Id);

        // Assert
        request.Participants.Should().NotContain(p => p.EmployeeId == colleague.Id);
    }

    [Fact]
    public async Task Resolve_NamesTheRequestsDeskAsTheSettingsScope()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(requester);

        // Act
        var access = await Resolve(request);

        // Assert
        access!.ScopeId.Should().Be(request.DeskId);
    }

    [Fact]
    public async Task ResolveSettings_ForADeskAdmin_Manages()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var access = await CreateSut().ResolveSettings(desk.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().Be(new DiscussionSettingsAccess(CanManage: true));
    }

    [Fact]
    public async Task ResolveSettings_ForAnAgent_ReadsButDoesNotManage()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: [agent.Id]);
        _scenario.SignedInAs(agent);

        // Act
        var access = await CreateSut().ResolveSettings(desk.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().Be(new DiscussionSettingsAccess(CanManage: false));
    }

    [Fact]
    public async Task ResolveSettings_ForTheDefault_IsTheDemandManagementAdminsToManage()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);
        var asDeskAdmin = await CreateSut().ResolveSettings(null, TestContext.Current.CancellationToken);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var asAdmin = await CreateSut().ResolveSettings(null, TestContext.Current.CancellationToken);

        // Assert
        asDeskAdmin.Should().Be(new DiscussionSettingsAccess(CanManage: false));
        asAdmin.Should().Be(new DiscussionSettingsAccess(CanManage: true));
    }

    [Fact]
    public async Task ResolveSettings_ForADeskThatDoesNotExist_IsNull()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var access = await CreateSut().ResolveSettings(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task ResolveSettings_WhileDemandManagementIsOff_IsNull()
    {
        // Arrange
        var desk = _scenario.Desk();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        TurnDemandManagement(on: false);

        // Act
        var access = await CreateSut().ResolveSettings(desk.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task OnPosted_AMentionByAParticipant_AddsNobody()
    {
        // Arrange
        var participant = _scenario.Employee();
        var outsider = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee(), participant.Id);
        _scenario.SignedInAs(participant);

        // Act
        await Post(request, participant.Id, DiscussionMessageKind.Public, outsider.Id);

        // Assert
        request.Participants.Should().ContainSingle().Which.EmployeeId.Should().Be(participant.Id);
    }

    [Fact]
    public async Task OnPosted_AMentionInAnInternalNote_AddsNobody()
    {
        // Arrange
        var agent = _scenario.Employee();
        var outsider = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open, agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        await Post(request, agent.Id, DiscussionMessageKind.InternalNote, outsider.Id);

        // Assert
        request.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task OnPosted_OnAPrivateDesk_AddsNobody()
    {
        // Arrange — participation on a private desk is granted by an agent, never accrued
        var agent = _scenario.Employee();
        var outsider = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        await Post(request, agent.Id, DiscussionMessageKind.Public, outsider.Id);

        // Assert
        request.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task OnPosted_MentioningSomeoneAlreadyOnTheRequest_AddsNobody()
    {
        // Arrange
        var requester = _scenario.Employee();
        var agent = _scenario.Employee();
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open, agentIds: [agent.Id]), requester, participant.Id);
        _scenario.SignedInAs(requester);

        // Act
        await Post(request, requester.Id, DiscussionMessageKind.Public, agent.Id, participant.Id);

        // Assert
        request.Participants.Should().ContainSingle().Which.EmployeeId.Should().Be(participant.Id);
    }


    [Fact]
    public async Task OnEdited_AMentionAddedByTheRequesterOnAnOpenDesk_MakesThePersonAParticipant()
    {
        // Arrange
        var requester = _scenario.Employee();
        var colleague = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(requester);

        // Act
        await EditToMention(request, requester.Id, DiscussionMessageKind.Public, colleague.Id);

        // Assert
        request.Participants.Should().ContainSingle(p => p.EmployeeId == colleague.Id && p.AddedById == requester.Id);
    }

    [Fact]
    public async Task OnEdited_ByAViewerWhoIsNotYetAParticipant_DoesNotAddTheAuthor()
    {
        // Arrange — editing only reaches the people it newly mentions
        var viewer = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(viewer, IntakeScenario.View);

        // Act
        await EditToMention(request, viewer.Id, DiscussionMessageKind.Public);

        // Assert
        request.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task OnEdited_AMentionAddedToAnInternalNote_AddsNobody()
    {
        // Arrange
        var agent = _scenario.Employee();
        var outsider = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open, agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        await EditToMention(request, agent.Id, DiscussionMessageKind.InternalNote, outsider.Id);

        // Assert
        request.Participants.Should().BeEmpty();
    }
}
