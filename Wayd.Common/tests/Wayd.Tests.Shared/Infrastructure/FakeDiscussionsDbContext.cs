using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Employees;
namespace Wayd.Tests.Shared.Infrastructure;

/// <summary>
/// An in-memory <see cref="IDiscussionsDbContext"/> for a module's handlers that read a record's conversation.
/// </summary>
public sealed class FakeDiscussionsDbContext : IDiscussionsDbContext
{
    private readonly List<DiscussionChannel> _channels = [];
    private readonly List<DiscussionMessage> _messages = [];
    private readonly List<Employee> _employees = [];

    public DbSet<DiscussionChannel> DiscussionChannels => _channels.AsDbSet();
    public DbSet<DiscussionMessage> DiscussionMessages => _messages.AsDbSet();
    public DbSet<Employee> Employees => _employees.AsDbSet();

    /// <summary>The number of times <see cref="SaveChangesAsync"/> has been called.</summary>
    public int SaveChangesCallCount { get; private set; }

    public void AddMessages(params DiscussionMessage[] messages) => _messages.AddRange(messages);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }
}
