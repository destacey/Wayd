/**
 * Type definition for JSON objects
 */
export type JsonObject = Record<string, any>;

/**
 * Behavioural hints surfaced to MCP clients alongside a tool, per the spec's
 * ToolAnnotations. Clients use these to decide what to confirm with the user
 * before running: a tool marked `destructiveHint` is expected to prompt.
 *
 * The hints are advisory — the client, not this server, enforces them — so they
 * complement server-side authorization rather than replacing it.
 */
export interface McpToolAnnotations {
    /** Human-readable title for the tool. */
    title?: string;
    /** The tool only reads state and never modifies it. */
    readOnlyHint?: boolean;
    /** The tool may perform destructive or otherwise hard-to-reverse updates. */
    destructiveHint?: boolean;
    /** Repeating the call with the same arguments has no additional effect. */
    idempotentHint?: boolean;
    /** The tool interacts with entities outside its own closed world. */
    openWorldHint?: boolean;
}

/**
 * Interface for MCP Tool Definition
 */
export interface McpToolDefinition {
    name: string;
    description: string;
    inputSchema: any;
    method: string;
    pathTemplate: string;
    executionParameters: { name: string; in: string }[];
    requestBodyContentType?: string;
    securityRequirements: any[];
    annotations?: McpToolAnnotations;
}
