namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Sent to every client when it connects, so clients that never load the published skills (Claude Desktop,
/// Cursor, ChatGPT, claude.ai connectors) still learn the rules that span all tools.
/// </summary>
/// <remarks>
/// Keep it to those cross-tool rules: per-area guidance belongs in tool descriptions and skills, since
/// clients may place this text in every prompt.
/// </remarks>
public static class McpServerInstructions
{
    /// <summary>The instructions text.</summary>
    public const string Text =
        "Wayd tools read and change live delivery data that other people rely on. " +
        "Updates are whole-record overwrites, not patches: read the record first and pass back every field that should stay, because an omitted field is cleared. " +
        "Role lists (sponsorIds, ownerIds, managerIds, memberIds) replace that role's membership, and an omitted or empty list removes everyone in it. " +
        "A parameter named idOrKey (or xxxIdOrKey) accepts a UUID or a key; any other id parameter takes a UUID only unless its description says it accepts a key, so look the UUID up with a read tool first. " +
        "Status changes, deletes and other destructive tools must have the user's explicit confirmation before you call them.";
}
