namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>A lifecycle action on an incident that moves its status.</summary>
public enum IncidentWorkflowAction
{
    /// <summary>Moves an open incident into active response.</summary>
    BeginResponding = 1,

    /// <summary>Records that service has been restored.</summary>
    MarkRecovered = 2,

    /// <summary>Closes the incident after recovery.</summary>
    Close = 3,

    /// <summary>Closes the incident without treating it as real.</summary>
    Cancel = 4,
}
