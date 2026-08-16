import type { McpToolDefinition } from '../types.js';

/**
 * Status transitions for portfolios, programs, projects, and strategic initiatives.
 *
 * Every tool here changes the published status of a record other people rely on, and
 * several have side effects beyond the status itself (portfolio dates, cascade rules).
 * They are all annotated `destructiveHint` so MCP clients confirm with a human before
 * executing, and none is idempotent — the domain rejects a repeat transition rather
 * than treating it as a no-op.
 */

/** Shared annotation: a human must approve before the status changes. */
const requiresConfirmation = {
  destructiveHint: true,
  readOnlyHint: false,
  idempotentHint: false,
} as const;

/** Path-only transition: `{id}` in, no request body. */
function transition(
  name: string,
  pathTemplate: string,
  title: string,
  description: string,
  idParamDescription: string
): [string, McpToolDefinition] {
  return [name, {
    name,
    description,
    inputSchema: { type: 'object', properties: { id: { type: 'string', format: 'uuid', description: idParamDescription } }, required: ['id'] },
    method: 'post',
    pathTemplate,
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title, ...requiresConfirmation },
  }];
}

const CONFIRM = 'Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.';
const LEADERSHIP = 'Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough.';

export const definitions: [string, McpToolDefinition][] = [

  // ---------------------------------------------------------------- Portfolios

  transition(
    'Portfolios_Activate',
    '/api/ppm/portfolios/{id}/activate',
    'Activate portfolio',
    `Activate a proposed portfolio. **This also sets the portfolio's start date to today, and the date cannot be backdated or changed by this call** — do not use it to fix up a portfolio that actually started earlier. Only proposed portfolios can be activated. ${LEADERSHIP} ${CONFIRM}`,
    'Portfolio ID.'
  ),

  transition(
    'Portfolios_Close',
    '/api/ppm/portfolios/{id}/close',
    'Close portfolio',
    `Close an active or on-hold portfolio. **This also sets the portfolio's end date to today, and the date cannot be backdated by this call.** Only active or on-hold portfolios can be closed. ${LEADERSHIP} ${CONFIRM}`,
    'Portfolio ID.'
  ),

  transition(
    'Portfolios_Archive',
    '/api/ppm/portfolios/{id}/archive',
    'Archive portfolio',
    `Archive a closed portfolio, removing it from active use. Only closed portfolios can be archived — close it first. ${LEADERSHIP} ${CONFIRM}`,
    'Portfolio ID.'
  ),

  // ------------------------------------------------------------------ Programs

  transition(
    'Programs_Activate',
    '/api/ppm/programs/{id}/activate',
    'Activate program',
    `Activate a proposed program. The program must already have a start and end date. ${LEADERSHIP} ${CONFIRM}`,
    'Program ID.'
  ),

  transition(
    'Programs_Complete',
    '/api/ppm/programs/{id}/complete',
    'Complete program',
    `Complete an active program. **Every project in the program must already be completed or canceled**, and the program must have a start and end date — otherwise the call is rejected. ${LEADERSHIP} ${CONFIRM}`,
    'Program ID.'
  ),

  transition(
    'Programs_Cancel',
    '/api/ppm/programs/{id}/cancel',
    'Cancel program',
    `Cancel a program. A proposed program can be canceled directly; cancelling an **active** program requires every project in it to already be completed or canceled. A completed or canceled program cannot be canceled again. ${LEADERSHIP} ${CONFIRM}`,
    'Program ID.'
  ),

  // ------------------------------------------------------------------ Projects

  transition(
    'Projects_Approve',
    '/api/ppm/projects/{id}/approve',
    'Approve project',
    `Approve a proposed project. A lifecycle must be assigned to the project first, and only proposed projects can be approved. ${LEADERSHIP} ${CONFIRM}`,
    'Project ID.'
  ),

  transition(
    'Projects_Activate',
    '/api/ppm/projects/{id}/activate',
    'Activate project',
    `Activate a proposed or approved project. The project must already have a start and end date. ${LEADERSHIP} ${CONFIRM}`,
    'Project ID.'
  ),

  transition(
    'Projects_Complete',
    '/api/ppm/projects/{id}/complete',
    'Complete project',
    `Complete an active project. The project must have a start and end date, and only active projects can be completed. ${LEADERSHIP} ${CONFIRM}`,
    'Project ID.'
  ),

  transition(
    'Projects_Cancel',
    '/api/ppm/projects/{id}/cancel',
    'Cancel project',
    `Cancel a project that is not already completed or canceled. ${LEADERSHIP} ${CONFIRM}`,
    'Project ID.'
  ),

  // ------------------------------------------------------- Strategic initiatives

  transition(
    'StrategicInitiatives_Approve',
    '/api/ppm/strategic-initiatives/{id}/approve',
    'Approve strategic initiative',
    `Approve a proposed strategic initiative. ${CONFIRM}`,
    'Strategic initiative ID.'
  ),

  transition(
    'StrategicInitiatives_Activate',
    '/api/ppm/strategic-initiatives/{id}/activate',
    'Activate strategic initiative',
    `Activate an approved strategic initiative. ${CONFIRM}`,
    'Strategic initiative ID.'
  ),

  transition(
    'StrategicInitiatives_Complete',
    '/api/ppm/strategic-initiatives/{id}/complete',
    'Complete strategic initiative',
    `Complete an active or on-hold strategic initiative. **Completing closes the initiative**, after which its KPIs and linked projects can no longer be added, edited, reordered, or removed. ${CONFIRM}`,
    'Strategic initiative ID.'
  ),

  transition(
    'StrategicInitiatives_Cancel',
    '/api/ppm/strategic-initiatives/{id}/cancel',
    'Cancel strategic initiative',
    `Cancel a strategic initiative. **Cancelling closes the initiative**, after which its KPIs and linked projects can no longer be added, edited, reordered, or removed. ${CONFIRM}`,
    'Strategic initiative ID.'
  ),

];
