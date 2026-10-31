/**
 * A stand-in for the Wayd MCP server that answers from a scenario's fixtures instead of a Wayd instance.
 *
 * It advertises what the hosted server does, from the saved catalogue (`catalog.ts`): every tool's name,
 * title, description, input schema and annotations, and the server instructions. Each call is answered
 * the way the hosted server words its answers: a fixture's result as compact JSON text, a fixture marked
 * `error` as an API error naming its status and problem, and a call no fixture covers as a 404. A call
 * that leaves out a required argument is refused before any fixture is read, as the hosted server refuses
 * it before calling the API. `Skills_Get` answers from the skills the run installed. Every call is appended
 * to a log, which is what the run is graded on.
 *
 * Configured by environment, since Claude Code launches it from an MCP config:
 * - `WAYD_EVAL_SCENARIOS` — the scenario file; `WAYD_EVAL_SCENARIO_ID` — the scenario in it
 * - `WAYD_EVAL_CALL_LOG` — the JSONL file each call is appended to
 * - `WAYD_EVAL_REPLIES` — how many scripted user replies the agent has received so far
 * - `WAYD_EVAL_SKILLS` — the directory `Skills_Get` reads skills from
 * - `WAYD_EVAL_CATALOG` — the catalogue, when it is not `catalog.json` beside this file
 */
import { appendFileSync, existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema, type CallToolResult } from '@modelcontextprotocol/sdk/types.js';

import { loadCatalog } from './catalog.js';
import { defaultFixture, fixtureFor, type Scenario } from './scenario.js';

const env = (name: string) => {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is not set`);
  return value;
};

const scenarioId = env('WAYD_EVAL_SCENARIO_ID');
const scenario = (JSON.parse(readFileSync(env('WAYD_EVAL_SCENARIOS'), 'utf8')) as Scenario[]).find((s) => s.id === scenarioId);
if (!scenario) throw new Error(`scenario ${scenarioId} not found`);
const callLog = env('WAYD_EVAL_CALL_LOG');
const repliesBefore = Number(env('WAYD_EVAL_REPLIES'));
const skillsDir = env('WAYD_EVAL_SKILLS');
const catalog = loadCatalog();
const tools = new Map(catalog.tools.map((t) => [t.name, t]));

/**
 * Every tool the agent has called in this run that reached the API, read from the log because Claude Code
 * starts a new server for each scripted reply. A call refused before the API changed nothing, so it does
 * not count towards a fixture's `after`.
 */
function calledSoFar(): Set<string> {
  if (!existsSync(callLog)) return new Set();
  return new Set(readFileSync(callLog, 'utf8').split('\n').filter(Boolean)
    .map((line) => JSON.parse(line) as { tool: string; reachedApi: boolean; isError?: boolean })
    .filter((c) => c.reachedApi && !c.isError)
    .map((c) => c.tool));
}

const text = (value: string, isError = false): CallToolResult => ({ content: [{ type: 'text', text: value }], ...(isError && { isError }) });

/** The hosted server's wording for a failed API call (`ApiToolResult.DescribeFailure`). */
function apiError(status: number, problem: unknown): CallToolResult {
  const reasons: Record<number, string> = { 400: 'Bad Request', 403: 'Forbidden', 404: 'Not Found', 409: 'Conflict', 422: 'Unprocessable Entity' };
  const p = (problem ?? {}) as { title?: string; detail?: string; errors?: Record<string, string[]> };
  const parts = [p.title, p.detail, ...Object.entries(p.errors ?? {}).map(([field, messages]) => `${field}: ${messages.join(' ')}`)];
  return text(`API Error: Status ${status} (${reasons[status] ?? 'Status text not available'}). ${parts.filter(Boolean).join(' ')}`, true);
}

/** `Skills_Get`: a skill's guide without its front matter, or one of its reference files. */
function getSkill(args: Record<string, unknown>): CallToolResult {
  const name = typeof args.name === 'string' ? args.name : '';
  const guide = join(skillsDir, name, 'SKILL.md');
  if (!/^wayd-[a-z-]+$/.test(name) || !existsSync(guide)) return text(`Invalid arguments for tool 'Skills_Get': name must name a skill.`, true);
  if (args.file === undefined || args.file === null) return text(readFileSync(guide, 'utf8').replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, '').trimStart());
  const file = String(args.file).replace(/\\/g, '/').replace(/^[./]+/, '');
  const path = join(skillsDir, name, file);
  if (file.includes('..') || !existsSync(path)) return text(`Invalid arguments for tool 'Skills_Get': file names no reference file of the skill.`, true);
  return text(readFileSync(path, 'utf8'));
}

const server = new Server(
  { name: catalog.server.name, version: catalog.server.version },
  { capabilities: { tools: {} }, instructions: catalog.instructions },
);

// No output schemas: a client checks structured content against them, and a fixture is the API's body as a
// scenario author wrote it, not a guaranteed match. The agent reads the text either way.
server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: catalog.tools.map(({ outputSchema: _outputSchema, ...tool }) => tool),
}));

server.setRequestHandler(CallToolRequestSchema, async (request): Promise<CallToolResult> => {
  const tool = request.params.name;
  const args = (request.params.arguments ?? {}) as Record<string, unknown>;
  const log = (entry: { fixtured: boolean; reachedApi: boolean; isError?: boolean }) =>
    appendFileSync(callLog, JSON.stringify({ tool, args, repliesBefore, ...entry }) + '\n');

  const definition = tools.get(tool);
  if (!definition) {
    log({ fixtured: false, reachedApi: false });
    return text(`Unknown tool: '${tool}'`, true);
  }
  const missing = (definition.inputSchema.required ?? []).find((name) => args[name] === undefined || args[name] === null);
  if (missing) {
    log({ fixtured: false, reachedApi: false, isError: true });
    return text(`Invalid arguments for tool '${tool}': ${missing} is required.`, true);
  }
  if (tool === 'Skills_Get') {
    const result = getSkill(args);
    log({ fixtured: true, reachedApi: false, isError: result.isError === true });
    return result;
  }

  const fixture = fixtureFor(scenario, tool, args, calledSoFar()) ?? defaultFixture(tool, args);
  const result = !fixture
    ? apiError(404, { title: 'Not Found', detail: 'No record matches the identifier given.' })
    : fixture.error
      ? apiError((fixture.result as { status?: number } | null)?.status ?? 400, fixture.result)
      : text(fixture.result === undefined || fixture.result === null ? '' : JSON.stringify(fixture.result));
  log({ fixtured: fixture !== undefined, reachedApi: true, isError: result.isError === true });
  return result;
});

await server.connect(new StdioServerTransport());
