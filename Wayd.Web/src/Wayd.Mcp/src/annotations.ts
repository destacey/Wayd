import type { McpToolAnnotations, McpToolDefinition } from './types.js';

/**
 * What a tool's HTTP method implies, before its own annotations are applied. Every write
 * starts out destructive, so a new write tool is never advertised as safe to run unconfirmed
 * unless its definition says so deliberately. PUT and DELETE are idempotent by HTTP's
 * definition; POST is not.
 */
const METHOD_DEFAULTS: Record<string, McpToolAnnotations> = {
  get: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
  put: { readOnlyHint: false, destructiveHint: true, idempotentHint: true },
  delete: { readOnlyHint: false, destructiveHint: true, idempotentHint: true },
};
const WRITE_DEFAULT: McpToolAnnotations = { readOnlyHint: false, destructiveHint: true, idempotentHint: false };

/**
 * The annotations advertised for a tool: its method's defaults, overridden by its own.
 * `openWorldHint` is false throughout, since every tool reaches only the Wayd API; the
 * spec's default of true would tell clients otherwise.
 */
export function annotationsFor(definition: McpToolDefinition): McpToolAnnotations {
  return {
    ...(METHOD_DEFAULTS[definition.method.toLowerCase()] ?? WRITE_DEFAULT),
    openWorldHint: false,
    ...definition.annotations,
  };
}
