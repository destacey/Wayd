namespace Wayd.Web.Api.Models.Admin.Messaging;

/// <summary>
/// Identifies the dead-lettered messages a replay or discard operation targets.
/// </summary>
public class DeadLetterMessagesRequest
{
    public List<Guid> Ids { get; set; } = [];
}
