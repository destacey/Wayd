using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Mirrors an external work process in the status workflow engine: a managed record type per work type, a
/// managed workflow per work type, a managed scheme for the process, and an assignment of that scheme to
/// every workspace on the process.
/// </summary>
/// <remarks>
/// Runs after the process itself is synced, so its workspaces are already linked to it. Idempotent: each
/// piece is found by its source key and brought into line.
/// </remarks>
public sealed record SyncExternalWorkProcessWorkflowsCommand(IExternalWorkProcessConfiguration WorkProcess) : ICommand;
