using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// The tool that hands an agent a published skill (<see cref="Skills"/>), so a model can read an area's
/// guidance on its own instead of waiting for the user to pick the matching prompt.
/// </summary>
/// <remarks>
/// <para>
/// Offered on every connection, whatever toolsets or read-only mode it chose, as the prompts are: the
/// guidance applies to whichever tools the connection has. Every other tool's description and failed result
/// name the skill to fetch (<see cref="Pointer"/>, <see cref="FailureNote"/>).
/// </para>
/// <para>
/// It answers with the markdown as text and declares no output schema. With one, the spec expects the text
/// to be the structured content serialized, so a client would hand the model either escaped JSON or the
/// skill twice; a skill is guidance to read, not data to parse.
/// </para>
/// </remarks>
public static class SkillTools
{
    /// <summary>The tool's name.</summary>
    public const string Name = "Skills_Get";

    /// <summary>The tool, describing <paramref name="skills"/>.</summary>
    public static McpServerTool Create(IReadOnlyList<Skill> skills) => new GetSkillTool(skills);

    /// <summary>The sentence a tool's description ends with, naming the skill that guides it.</summary>
    public static string Pointer(string skill) => $"Guidance for this area: {Name} with name \"{skill}\".";

    /// <summary>The note a failed call's result carries, pointing back to the skill that guides the tool.</summary>
    public static string FailureNote(string skill) =>
        $"If the request itself was wrong, read the {skill} skill before retrying ({Name} with name \"{skill}\"): it explains how these tools fit together.";

    /// <summary><c>Skills_Get</c>: a skill's guide, or one of its reference files.</summary>
    private sealed class GetSkillTool : McpServerTool
    {
        private readonly Dictionary<string, Skill> _skills;

        /// <summary>The tool, whose <c>name</c> argument accepts exactly <paramref name="skills"/>.</summary>
        public GetSkillTool(IReadOnlyList<Skill> skills)
        {
            _skills = skills.ToDictionary(s => s.Name, StringComparer.Ordinal);

            // Only each description's first sentence, which names the tools the skill covers: the rest is
            // for clients that choose a skill to load, and every tool already names its own.
            var catalogue = string.Join(' ', skills.Select(s => $"`{s.Name}`: {FirstSentence(s.Description)}"));
            ProtocolTool = new Tool
            {
                Name = Name,
                Title = "Get a skill",
                Description = "Get a Wayd skill: guidance on how an area's tools fit together — which to call in what order, how to resolve ids, and the rules the tools enforce. Read the matching skill before you first work in an area, and its reference file before the task that file covers. Answers with the skill's guide as markdown, or with one of its reference files when `file` names one; the guide links each reference file by the path `file` takes. Skills: " + catalogue,
                InputSchema = JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["name"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray([.. skills.Select(s => (JsonNode)s.Name)]),
                            ["description"] = "The skill, by name.",
                        },
                        ["file"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional reference file of the skill, by the path its guide links (`reference/tasks.md`). Omit it for the guide.",
                        },
                    },
                    ["required"] = new JsonArray("name"),
                }),
                Annotations = McpToolAnnotationDefaults.For("GET", "Get a skill"),
            };
        }

        /// <inheritdoc />
        public override Tool ProtocolTool { get; }

        /// <inheritdoc />
        public override IReadOnlyList<object> Metadata { get; } = [];

        /// <inheritdoc />
        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            var arguments = request.Params?.Arguments;
            if (arguments?.TryGetValue("name", out var nameValue) != true
                || nameValue.ValueKind != JsonValueKind.String
                || !_skills.TryGetValue(nameValue.GetString()!, out var skill))
            {
                return ValueTask.FromResult(ApiToolResult.Error(
                    $"Invalid arguments for tool '{Name}': name must be one of {string.Join(", ", _skills.Keys)}."));
            }

            if (!arguments.TryGetValue("file", out var fileValue) || fileValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return ValueTask.FromResult(ApiToolResult.Text(skill.Body));

            var reference = fileValue.ValueKind == JsonValueKind.String
                ? skill.References.SingleOrDefault(r => r.Path == fileValue.GetString()!.Replace('\\', '/').TrimStart('.', '/'))
                : null;
            if (reference is not null)
                return ValueTask.FromResult(ApiToolResult.Text(reference.Text));

            var known = skill.References.Count == 0
                ? $"The {skill.Name} skill has no reference files; omit file."
                : $"The {skill.Name} skill's files are {string.Join(", ", skill.References.Select(r => r.Path))}.";
            return ValueTask.FromResult(ApiToolResult.Error($"Invalid arguments for tool '{Name}': file names no reference file of the skill. {known}"));
        }

        /// <summary><paramref name="text"/> up to the end of its first sentence, or all of it when it has one.</summary>
        private static string FirstSentence(string text)
        {
            var end = text.IndexOf(". ", StringComparison.Ordinal);
            return end < 0 ? text : text[..(end + 1)];
        }
    }
}
