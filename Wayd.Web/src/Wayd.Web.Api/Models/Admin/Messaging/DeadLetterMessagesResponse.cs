namespace Wayd.Web.Api.Models.Admin.Messaging;

/// <summary>
/// A page of dead-lettered messages. <c>PageNumber</c> is 0-based, mirroring Wolverine's
/// DeadLetterEnvelopeQuery paging.
/// </summary>
public class DeadLetterMessagesResponse
{
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public List<DeadLetterMessageResponse> Items { get; set; } = [];
}
