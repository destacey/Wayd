using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// A test double for IWaydDbContext that provides in-memory collections for all DbSets.
/// This eliminates the need for complex Moq setups in tests and is reusable across all Common domain tests.
/// </summary>
public class FakeWaydDbContext : IWaydDbContext, IStatusWorkflowDbContext, INotificationsDbContext, IDiscussionsDbContext, IActivityLogDbContext, IDisposable
{
    // Common domain entities
    private readonly List<Employee> _employees = [];
    private readonly List<ExternalEmployeeBlacklistItem> _externalEmployeeBlacklistItems = [];
    private readonly List<ExternalIdentityMapping> _externalIdentityMappings = [];
    private readonly List<OidcProvider> _oidcProviders = [];
    private readonly List<PersonalAccessToken> _personalAccessTokens = [];
    private readonly List<User> _waydUsers = [];
    private readonly List<ScoringModel> _scoringModels = [];
    private readonly List<ActivityLogEntry> _activityLogs = [];

    // The workflow engine's tables, for handlers that reach IStatusWorkflowDbContext.
    private readonly List<StatusWorkflow> _statusWorkflows = [];
    private readonly List<WorkflowAssignment> _workflowAssignments = [];
    private readonly List<StatusWorkflowScheme> _statusWorkflowSchemes = [];
    private readonly List<RecordType> _recordTypes = [];
    private readonly List<WorkflowAliasName> _workflowAliasNames = [];
    private readonly List<StatusTransition> _statusTransitions = [];

    // Following, for handlers that reach INotificationsDbContext.
    private readonly List<Follow> _follows = [];
    private readonly List<Notification> _notifications = [];
    private readonly List<RecordMute> _recordMutes = [];
    private readonly List<ReasonMute> _reasonMutes = [];

    // Discussions, for handlers that reach IDiscussionsDbContext. Messages carry their mentions and revisions in
    // memory, so `.Include` is a no-op here; only an integration test catches a missing one.
    private readonly List<DiscussionChannel> _discussionChannels = [];
    private readonly List<DiscussionMessage> _discussionMessages = [];
    private readonly List<DiscussionSettings> _discussionSettings = [];

    // DbSet properties
    public DbSet<Employee> Employees => _employees.AsDbSet();
    public DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems => _externalEmployeeBlacklistItems.AsDbSet();
    public DbSet<ExternalIdentityMapping> ExternalIdentityMappings => _externalIdentityMappings.AsDbSet();
    public DbSet<OidcProvider> OidcProviders => _oidcProviders.AsDbSet();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => _personalAccessTokens.AsDbSet();
    public DbSet<User> WaydUsers => _waydUsers.AsDbSet();
    public DbSet<ScoringModel> ScoringModels => _scoringModels.AsDbSet();
    public DbSet<ActivityLogEntry> ActivityLogs => _activityLogs.AsDbSet();

    public DbSet<StatusWorkflow> StatusWorkflows => _statusWorkflows.AsDbSet();
    public DbSet<WorkflowAssignment> WorkflowAssignments => _workflowAssignments.AsDbSet();
    public DbSet<StatusWorkflowScheme> StatusWorkflowSchemes => _statusWorkflowSchemes.AsDbSet();
    public DbSet<RecordType> RecordTypes => _recordTypes.AsDbSet();
    public DbSet<WorkflowAliasName> WorkflowAliasNames => _workflowAliasNames.AsDbSet();
    public DbSet<StatusTransition> StatusTransitions => _statusTransitions.AsDbSet();

    public DbSet<Follow> Follows => _follows.AsDbSet();
    public DbSet<Notification> Notifications => _notifications.AsDbSet();
    public DbSet<RecordMute> RecordMutes => _recordMutes.AsDbSet();
    public DbSet<ReasonMute> ReasonMutes => _reasonMutes.AsDbSet();

    public DbSet<DiscussionChannel> DiscussionChannels => _discussionChannels.AsDbSet();
    public DbSet<DiscussionMessage> DiscussionMessages => _discussionMessages.AsDbSet();
    public DbSet<DiscussionSettings> DiscussionSettings => _discussionSettings.AsDbSet();

    // ChangeTracker - we can't create a real one, so we return null and the handler uses defensive coding (_dbContext.ChangeTracker?.Clear())
    public ChangeTracker ChangeTracker => null!;

    public DatabaseFacade Database => throw new NotImplementedException("Database operations are not supported in FakeWaydDbContext. Use integration tests with a real DbContext for database-specific functionality.");

    /// <summary>
    /// Gets the number of times SaveChangesAsync has been called.
    /// Useful for asserting that the handler saved changes at the expected times.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    /// <summary>
    /// An exception the next save throws instead of saving, as a real context does on a unique-index or row-version
    /// collision. Cleared once thrown.
    /// </summary>
    public Exception? FailNextSaveWith { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        if (FailNextSaveWith is { } failure)
        {
            FailNextSaveWith = null;
            throw failure;
        }

        // Return the total number of entities as a simple success indicator
        var count = _employees.Count + _externalEmployeeBlacklistItems.Count + _externalIdentityMappings.Count + _personalAccessTokens.Count;
        return Task.FromResult(count);
    }

    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
    {
        // For unit tests, entity tracking is typically not needed
        // If you need this for specific tests, consider using integration tests with a real DbContext
        throw new NotImplementedException("Entry tracking is not needed for unit tests. If you need this, consider using integration tests with a real DbContext.");
    }

    public EntityEntry Entry(object entity)
    {
        throw new NotImplementedException("Entry tracking is not needed for unit tests. If you need this, consider using integration tests with a real DbContext.");
    }

    #region Helper Methods for Test Setup

    // Note for anyone writing workflow tests: a workflow added here carries its statuses in memory, so
    // `.Include(w => w.Statuses)` is a no-op and a handler that omits it still passes. Only an
    // integration test against a real context can catch that.
    public void AddStatusWorkflow(StatusWorkflow workflow) => _statusWorkflows.Add(workflow);
    public void AddStatusWorkflows(IEnumerable<StatusWorkflow> workflows) => _statusWorkflows.AddRange(workflows);
    public void AddWorkflowAssignment(WorkflowAssignment assignment) => _workflowAssignments.Add(assignment);

    // Added directly, because a transition appended by ApplyStatus only reaches the set through
    // BaseDbContext's drain on save, which this fake does not run.
    public void AddStatusTransition(StatusTransition transition) => _statusTransitions.Add(transition);
    public void AddStatusTransitions(IEnumerable<StatusTransition> transitions) => _statusTransitions.AddRange(transitions);

    public void AddWaydUser(User user) => _waydUsers.Add(user);

    public void AddEmployee(Employee employee) => _employees.Add(employee);
    public void AddFollow(Follow follow) => _follows.Add(follow);
    public void AddNotification(Notification notification) => _notifications.Add(notification);
    public void AddRecordMute(RecordMute mute) => _recordMutes.Add(mute);
    public void AddReasonMute(ReasonMute mute) => _reasonMutes.Add(mute);
    public void AddDiscussionChannel(DiscussionChannel channel) => _discussionChannels.Add(channel);
    public void AddDiscussionMessage(DiscussionMessage message) => _discussionMessages.Add(message);
    public void AddDiscussionSettings(DiscussionSettings settings) => _discussionSettings.Add(settings);

    /// <summary>Removes a notification, as the store's set-based writes would replace or delete it.</summary>
    public void RemoveNotification(Notification notification) => _notifications.Remove(notification);

    #endregion Helper Methods for Test Setup

    public void Dispose()
    {
        // Nothing to dispose for in-memory collections
        GC.SuppressFinalize(this);
    }
}
