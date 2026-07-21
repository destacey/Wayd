using System.Runtime.Serialization;

namespace Wayd.Integrations.AzureDevOps.Models.WorkItems;

[DataContract]
internal sealed record WorkItemsBatchRequest
{
    [DataMember]
    public string[] Fields { get; set; } = [];

    [DataMember]
    public int[] Ids { get; set; } = [];

    // The API's default error policy is "fail", which rejects the ENTIRE batch when any requested
    // id no longer exists — a real race during sync, since ids come from an earlier WIQL query and
    // work items can be deleted in between. "omit" returns the survivors and skips the rest;
    // deletions are reconciled separately via GetDeletedWorkItemIds.
    [DataMember]
    public string ErrorPolicy { get; set; } = "omit";

    public static WorkItemsBatchRequest Create(IEnumerable<int> ids, IEnumerable<string> fields)
    {
        return new WorkItemsBatchRequest
        {
            Ids = ids.ToArray(),
            Fields = fields.ToArray()
        };
    }
}
