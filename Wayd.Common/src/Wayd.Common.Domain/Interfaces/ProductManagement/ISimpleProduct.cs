namespace Wayd.Common.Domain.Interfaces.ProductManagement;

/// <summary>
/// The identifying shape of a product node, carried on events so a consumer can act without a database
/// round trip.
/// </summary>
/// <remarks>
/// Add a field only when a projection consumer filters or renders on it. Type and parent are absent
/// deliberately: a bare <c>ProductTypeId</c> is unresolvable without the type table (the useful flag,
/// releasable, arrives in phase two), and a single <c>ParentId</c> is one hop rather than the ancestry
/// a tree question needs.
/// </remarks>
public interface ISimpleProduct
{
    Guid Id { get; }
    int Key { get; }
    string Name { get; }
    string? Description { get; }
}
