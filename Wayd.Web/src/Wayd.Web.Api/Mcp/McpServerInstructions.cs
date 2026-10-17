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
        "Confirm every change with the user before you make it: before a call that creates, updates, tags, moves, changes the status of, or deletes anything, say what will change and wait for their go-ahead, then do only what they approved. " +
        "Updates are whole-record overwrites, not patches: read the record first and pass back every field that should stay, because an omitted field is cleared. " +
        "Every list on a write replaces the whole set: role lists (sponsorIds, ownerIds, managerIds, memberIds) replace that role's membership, and lists such as assigneeIds, strategicThemeIds, personaIds, versionIds and packageIds replace theirs, so an omitted or empty list removes everything in it. " +
        "Role, assignee and employeeId parameters take employee ids, not user ids, unless the parameter's description says it takes a user id. " +
        "A parameter named idOrKey (or xxxIdOrKey) accepts a UUID or a key; any other id parameter takes a UUID only unless its description says it accepts a key, so look the UUID up with a read tool first. " +
        "Send a moment as an ISO 8601 datetime with its offset, exactly as reported and never converted to a local date; a calendar date is yyyy-MM-dd.";
}
