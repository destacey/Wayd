/**
 * A stand-in for the Wayd MCP server that answers from a scenario's fixtures instead of a Wayd instance.
 *
 * It advertises exactly what the real server does — every tool's name, description, schema and annotations,
 * and the server instructions — and runs each call through the real executor, so argument validation,
 * preconditions and error messages are the ones a user's agent meets. Only the HTTP API underneath is fake:
 * a local server that answers each request from the fixture matching the tool call that sent it. Every call
 * is appended to a log, which is what the run is graded on.
 *
 * Configured by environment, since Claude Code launches it from an MCP config:
 * - `WAYD_EVAL_SCENARIOS` — the scenario file; `WAYD_EVAL_SCENARIO_ID` — the scenario in it
 * - `WAYD_EVAL_CALL_LOG` — the JSONL file each call is appended to
 * - `WAYD_EVAL_REPLIES` — how many scripted user replies the agent has received so far
 */
import { appendFileSync, existsSync, readFileSync } from 'node:fs';
import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import type { AddressInfo } from 'node:net';
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema, type CallToolResult } from '@modelcontextprotocol/sdk/types.js';

import type { McpToolDefinition } from '../build/types.js';
import { defaultFixture, fixtureFor, type Fixture, type Scenario } from './scenario.js';

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

/** A tool call in flight: the fake API reads the fixture for it, and records that the API was reached. */
interface PendingCall {
  definition: McpToolDefinition;
  fixture: Fixture | undefined;
  reachedApi: boolean;
}
const pending = new Map<string, PendingCall>();

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
const notFound = { title: 'Not Found', status: 404, detail: 'No record matches the identifier given.' };

function send(res: ServerResponse, status: number, body: unknown) {
  if (body === undefined || body === null) {
    res.writeHead(status === 200 ? 204 : status).end();
    return;
  }
  const json = typeof body === 'string';
  res.writeHead(status, { 'content-type': json ? 'text/plain' : 'application/problem+json' }).end(json ? body : JSON.stringify(body));
}

const api = createServer((req: IncomingMessage, res: ServerResponse) => {
  const match = /^\/__call\/([^/]+)(\/[^?]*)/.exec(req.url ?? '');
  const call = match ? pending.get(match[1]!) : undefined;
  req.resume();
  if (!call) return send(res, 404, notFound);
  // A precondition GET reads an endpoint some other tool also reads, so it gets that tool's fixture, as the
  // real API would give the same answer to both. The agent did not make the call, so it is not logged.
  const precondition = call.definition.precondition;
  if (precondition && match![2] === precondition.path) {
    const reader = [...toolDefinitionMap.values()].find((d) => d.method.toLowerCase() === 'get' && d.pathTemplate === precondition.path);
    const answer = reader && fixtureFor(scenario, reader.name, {}, calledSoFar());
    if (!answer || answer.error) return send(res, 404, notFound);
    res.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify(answer.result));
    return;
  }
  call.reachedApi = true;
  if (!call.fixture) return send(res, 404, notFound);
  if (call.fixture.error) {
    const status = (call.fixture.result as { status?: number } | null)?.status ?? 400;
    return send(res, status, call.fixture.result);
  }
  // The real API answers JSON; send it as such so the executor formats it exactly as it would a real body.
  if (call.fixture.result === undefined || call.fixture.result === null) return send(res, 204, null);
  res.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify(call.fixture.result));
});
await new Promise<void>((resolve) => api.listen(0, '127.0.0.1', resolve));

// The executor reads the base URL and key when its module loads, so both are set before importing it.
process.env.WAYD_API_BASE_URL = `http://127.0.0.1:${(api.address() as AddressInfo).port}`;
process.env.WAYD_API_KEY = 'eval-stub';
const { toolDefinitionMap } = await import('../build/tools/index.js');
const { annotationsFor } = await import('../build/annotations.js');
const { SERVER_INSTRUCTIONS } = await import('../build/instructions.js');
const { SERVER_NAME, SERVER_VERSION } = await import('../build/config.js');
const { executeApiTool, securitySchemes } = await import('../build/executor.js');

/** The definition with every path it sends to prefixed by the call's id, so the fake API knows the call. */
function routed(definition: McpToolDefinition, callId: string): McpToolDefinition {
  const prefix = (path: string) => `/__call/${callId}${path}`;
  return {
    ...definition,
    pathTemplate: prefix(definition.pathTemplate),
    ...(definition.pathSelector && {
      pathSelector: {
        ...definition.pathSelector,
        paths: Object.fromEntries(Object.entries(definition.pathSelector.paths).map(([k, p]) => [k, prefix(p)])),
      },
    }),
    ...(definition.precondition && { precondition: { ...definition.precondition, path: prefix(definition.precondition.path) } }),
  };
}

const server = new Server(
  { name: SERVER_NAME, version: SERVER_VERSION },
  { capabilities: { tools: {} }, instructions: SERVER_INSTRUCTIONS },
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [...toolDefinitionMap.values()].map((def) => ({
    name: def.name,
    description: def.description,
    inputSchema: def.inputSchema,
    annotations: annotationsFor(def),
  })),
}));

let nextCallId = 0;
server.setRequestHandler(CallToolRequestSchema, async (request): Promise<CallToolResult> => {
  const tool = request.params.name;
  const args = (request.params.arguments ?? {}) as Record<string, unknown>;
  const definition = toolDefinitionMap.get(tool);
  if (!definition) {
    appendFileSync(callLog, JSON.stringify({ tool, args, repliesBefore, fixtured: false, reachedApi: false }) + '\n');
    return { content: [{ type: 'text', text: `Error: Unknown tool requested: ${tool}` }], isError: true };
  }

  const callId = String(nextCallId++);
  const fixture = fixtureFor(scenario, tool, args, calledSoFar()) ?? defaultFixture(tool, args);
  const call: PendingCall = { definition, fixture, reachedApi: false };
  pending.set(callId, call);
  try {
    // A local handler answers without the API, exactly as it does in the real server.
    const result = definition.localHandler
      ? definition.localHandler(args)
      : await executeApiTool(tool, routed(definition, callId), args, securitySchemes);
    appendFileSync(callLog, JSON.stringify({
      tool, args, repliesBefore,
      fixtured: call.fixture !== undefined,
      reachedApi: call.reachedApi,
      isError: result.isError === true,
    }) + '\n');
    return result;
  } finally {
    pending.delete(callId);
  }
});

await server.connect(new StdioServerTransport());
