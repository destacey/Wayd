using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Attachments;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>An in-memory <see cref="IAttachmentsDbContext"/>.</summary>
public sealed class FakeAttachmentsDbContext : IAttachmentsDbContext
{
    private readonly List<Attachment> _attachments = [];

    public DbSet<Attachment> Attachments => _attachments.AsDbSet();

    /// <summary>Every attachment held, as the database would after the last save.</summary>
    public IReadOnlyList<Attachment> All => _attachments;

    /// <summary>The number of times <see cref="SaveChangesAsync"/> has been called.</summary>
    public int SaveChangesCallCount { get; private set; }

    /// <summary>The save, counting from one, that fails as a concurrent change would; null for none.</summary>
    public int? ConflictOnSave { get; set; }

    public void Add(params Attachment[] attachments) => _attachments.AddRange(attachments);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        if (SaveChangesCallCount == ConflictOnSave)
        {
            throw new DbUpdateConcurrencyException("An attachment changed since it was read.");
        }

        return Task.FromResult(0);
    }
}

/// <summary>An in-memory <see cref="IAttachmentStore"/>.</summary>
public sealed class FakeAttachmentStore : IAttachmentStore
{
    /// <summary>The bytes held, by attachment id.</summary>
    public Dictionary<Guid, byte[]> Data { get; } = [];

    public void Write(Guid id, byte[] data) => Data[id] = data;

    public Task Copy(Guid sourceId, Guid targetId, CancellationToken cancellationToken)
    {
        Data[targetId] = Data[sourceId];
        return Task.CompletedTask;
    }

    public Task<byte[]?> Read(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Data.TryGetValue(id, out var data) ? data : null);

    public void Delete(Guid id) => Data.Remove(id);
}
