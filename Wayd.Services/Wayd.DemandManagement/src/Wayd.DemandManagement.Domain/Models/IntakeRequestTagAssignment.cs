using Wayd.Common.Domain.Tagging;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One tag applied to one intake request.
/// </summary>
/// <remarks>
/// Carries the tag's axis so that a single-value axis can be enforced, and a request filtered by axis,
/// without loading the tag.
/// </remarks>
public sealed class IntakeRequestTagAssignment : ITagAssignment
{
    private IntakeRequestTagAssignment() { }

    internal IntakeRequestTagAssignment(Guid requestId, Guid tagId, Guid? categoryId)
    {
        RequestId = requestId;
        TagId = tagId;
        CategoryId = categoryId;
    }

    /// <summary>The request carrying the tag.</summary>
    public Guid RequestId { get; private init; }

    /// <summary>The tag it carries.</summary>
    public Guid TagId { get; private init; }

    /// <summary>The tag's axis, denormalized from the tag. Null for a free tag.</summary>
    public Guid? CategoryId { get; private init; }
}
