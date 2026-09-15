import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

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

/** One column of an import file, as the API's OpenAPI document describes it. */
export interface ImportColumn {
    /** The header exactly as the file must spell it. */
    name: string;
    type: 'text' | 'id' | 'date' | 'timestamp' | 'integer' | 'number' | 'boolean';
    /** Whether a row must fill the cell. The header itself is always needed. */
    required: boolean;
    description?: string;
    /** The only values the cell accepts, where it names one of a fixed set. */
    values?: string[];
    maxLength?: number;
}

/** One kind of CSV import: the endpoint it is posted to and every file it takes. */
export interface ImportFormat {
    path: string;
    description?: string;
    files: { field: string; label?: string; required: boolean; columns: ImportColumn[] }[];
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
    /**
     * Where each argument goes: `path`, `query`, `header`, or `formFile` — a multipart part holding the
     * argument's text as a CSV file named for the argument.
     */
    executionParameters: { name: string; in: string }[];
    /**
     * For one tool fronting several endpoints that take the same request: the argument whose value picks
     * the path, in place of `pathTemplate`. Validate that argument as an enum of the keys, so no value can
     * reach a route the tool does not name.
     */
    pathSelector?: { parameter: string; paths: Record<string, string> };
    /** Query values sent on every call, which no argument can set or override. */
    fixedQuery?: Record<string, string | number | boolean>;
    /** Answers the call from the server itself, without a request to the API. */
    localHandler?: (args: JsonObject) => CallToolResult;
    requestBodyContentType?: string;
    securityRequirements: any[];
    annotations?: McpToolAnnotations;
}
