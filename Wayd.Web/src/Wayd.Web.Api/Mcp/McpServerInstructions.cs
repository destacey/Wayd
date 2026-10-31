namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Sent to every client when it connects, so clients that never load the published skills (Claude Desktop,
/// Cursor, ChatGPT, claude.ai connectors) still learn the rules that span all tools.
/// </summary>
/// <remarks>
/// Keep it to those cross-tool rules, plus the names of the workflows whose recipe a tool description
/// carries, so a model knows to look there: per-area guidance belongs in tool descriptions and skills, since
/// clients may place this text in every prompt.
/// </remarks>
public static class McpServerInstructions
{
    /// <summary>The instructions the server sends: the cross-tool rules, how to fetch a skill, and how to find the caller.</summary>
    public const string Text = Rules + " " + SkillsGuidance + " " + SelfGuidance;

    /// <summary>The rules that hold for every tool: confirmation, whole-record writes, ids and dates.</summary>
    private const string Rules =
        "Wayd tools read and change live delivery data that other people rely on. " +
        "Confirm every change with the user before you make it: before a call that creates, updates, tags, moves, changes the status of, or deletes anything, say what will change and wait for their go-ahead, then do only what they approved. " +
        "Updates are whole-record overwrites, not patches: read the record first and pass back every field that should stay, because an omitted field is cleared. " +
        "Every list on a write replaces the whole set: role lists (sponsorIds, ownerIds, managerIds, memberIds) replace that role's membership, and lists such as assigneeIds, strategicThemeIds, personaIds, versionIds and packageIds replace theirs, so an omitted or empty list removes everything in it. " +
        "Role, assignee and employeeId parameters take employee ids, not user ids, unless the parameter's description says it takes a user id. " +
        "A parameter named idOrKey (or xxxIdOrKey) accepts a UUID or a key; any other id parameter takes a UUID only unless its description says it accepts a key, so look the UUID up with a read tool first. " +
        "Send a moment as an ISO 8601 datetime with its offset, exactly as reported and never converted to a local date; a calendar date is yyyy-MM-dd. " +
        "Recommending and logging a project or PI objective health check follows a recipe in the description of Projects_CreateProjectHealthCheck or PlanningIntervals_CreateObjectiveHealthCheck, and of the matching health-check evidence tool where one is listed: read it before you gather evidence.";

    /// <summary>
    /// How an agent answers a question about the person asking. Without it, agents list users or employees
    /// to find the caller, which most callers are not permitted to do.
    /// </summary>
    private const string SelfGuidance =
        "When a request is about the person asking (my work items, my teams, projects I manage), never list users or employees to find them: call Me_GetProfile, whose employee id is the one employeeId, role and assignee parameters take for them, or use a Me_ tool or a tool that defaults to the caller.";

    /// <summary>How an agent finds the skill for the area it works in.</summary>
    private const string SkillsGuidance =
        "Each tool's description ends by naming the skill that guides its area: call " + SkillTools.Name + " with that name before you first work in an area, and read the reference file the skill points to before the task it covers.";
}
