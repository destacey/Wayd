namespace Wayd.Work.Application.WorkProcesses;

/// <summary>
/// The source keys the work sync gives what it keeps in the status workflow engine.
/// </summary>
/// <remarks>
/// <strong>Never change a format once shipped</strong>: the sync finds each managed workflow, scheme and
/// record type by its key, so a new format would create duplicates beside the old ones.
/// <para>
/// Keys are built from the process's external id rather than the connection's, because Wayd keeps one
/// work process per external id: Azure DevOps' built-in processes share one id across organizations.
/// </para>
/// </remarks>
public static class ManagedWorkflowKeys
{
    /// <summary>The scheme mirroring a work process.</summary>
    public static string Scheme(Guid workProcessExternalId) => $"work-process:{workProcessExternalId:D}";

    /// <summary>
    /// The workflow one work type follows in a work process, by the type's stable source key, so renaming
    /// the type renames the workflow rather than starting another.
    /// </summary>
    public static string Workflow(Guid workProcessExternalId, string workTypeExternalKey) =>
        $"{WorkflowPrefix(workProcessExternalId)}{workTypeExternalKey.Trim()}";

    /// <summary>What every workflow key of a work process starts with.</summary>
    public static string WorkflowPrefix(Guid workProcessExternalId) => $"work-process:{workProcessExternalId:D}:";

    /// <summary>
    /// The record type for a work type. Its name, not its source key, because Work keeps one work type per
    /// name across every process and source: two processes' "Bug" types carry different source keys but are
    /// one work type. A type renamed at the source therefore becomes a new record type, as it becomes a new
    /// work type.
    /// </summary>
    public static string RecordType(string workTypeName) => workTypeName.Trim();

    /// <summary>
    /// A status within its workflow, by the state's stable source key, so renaming the state renames the
    /// status and keeps the category administrators mapped it to.
    /// </summary>
    public static string Status(string stateExternalKey) => stateExternalKey.Trim();
}
