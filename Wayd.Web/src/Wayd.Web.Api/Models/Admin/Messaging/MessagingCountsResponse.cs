namespace Wayd.Web.Api.Models.Admin.Messaging;

/// <summary>
/// Snapshot of the Wolverine durable message store: how many envelopes are currently persisted in
/// each lifecycle bucket. In a healthy system incoming/outgoing hover near zero — envelopes only
/// linger during backlog, scheduled delivery, or after an unclean shutdown.
/// </summary>
public class MessagingCountsResponse
{
    public int Incoming { get; set; }
    public int Scheduled { get; set; }
    public int Outgoing { get; set; }
    public int Handled { get; set; }
    public int DeadLetter { get; set; }
}
