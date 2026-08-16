/**
 * Protocol-level tests: spawn the built server exactly as an MCP client would
 * and assert it speaks clean JSON-RPC over stdio.
 *
 * These run against build/ (the published artifact), not src/, because the
 * failure mode they guard is a *runtime* one that type-checking cannot see:
 * anything a dependency writes to stdout corrupts the protocol stream. The
 * dotenv 17 upgrade did exactly that (it prints a startup banner unless
 * `quiet: true`), and lint/typecheck/build all passed while the server was
 * unusable by every stdio client.
 */
import { test, describe, before } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const serverEntry = join(__dirname, '../build/index.js');

/** Env that satisfies the server's required config without hitting a real API. */
const testEnv = {
  ...process.env,
  WAYD_API_BASE_URL: 'https://api.invalid',
  WAYD_API_KEY: 'test-key-not-a-real-secret',
};

interface JsonRpcResponse {
  jsonrpc: string;
  id?: number;
  result?: any;
  error?: { code: number; message: string };
}

interface ServerRun {
  /** Every line the server wrote to stdout. */
  stdoutLines: string[];
  /** Parsed JSON-RPC responses, keyed by request id. */
  responses: Map<number, JsonRpcResponse>;
  stderr: string;
}

const INIT_REQUEST = {
  jsonrpc: '2.0',
  id: 1,
  method: 'initialize',
  params: {
    protocolVersion: '2024-11-05',
    capabilities: {},
    clientInfo: { name: 'wayd-mcp-tests', version: '1.0.0' },
  },
};

/**
 * Splits accumulated stdout into complete, newline-terminated lines, discarding any
 * trailing partial line still being written.
 */
function completeLines(buffer: string): string[] {
  const lastNewline = buffer.lastIndexOf('\n');
  if (lastNewline === -1) return [];
  return buffer.slice(0, lastNewline).split('\n').filter(Boolean);
}

/**
 * Spawns the server, performs the MCP handshake, sends `requests`, and
 * collects everything written to stdout/stderr.
 */
async function runServer(
  requests: object[] = [],
  { env = testEnv }: { env?: NodeJS.ProcessEnv } = {}
): Promise<ServerRun> {
  const child: ChildProcessWithoutNullStreams = spawn(process.execPath, [serverEntry], {
    env,
    stdio: ['pipe', 'pipe', 'pipe'],
  });

  let stdout = '';
  let stderr = '';
  child.stdout.setEncoding('utf8');
  child.stderr.setEncoding('utf8');
  child.stdout.on('data', (chunk: string) => { stdout += chunk; });
  child.stderr.on('data', (chunk: string) => { stderr += chunk; });

  const expectedResponses = 1 + requests.length;

  // Resolves once we've seen a response line per request, or the timeout fires.
  const settled = new Promise<void>((resolve) => {
    const done = () => resolve();
    const timer = setTimeout(done, 10_000);
    timer.unref?.();
    child.stdout.on('data', () => {
      // Count only newline-TERMINATED lines. A large response (tools/list is tens of
      // kilobytes) arrives across several chunks, and a chunk that ends mid-line would
      // otherwise be counted as complete — resolving early and killing the child
      // partway through writing, which surfaces as "non-JSON line on stdout".
      if (completeLines(stdout).length >= expectedResponses) {
        clearTimeout(timer);
        done();
      }
    });
    child.on('exit', () => { clearTimeout(timer); done(); });
  });

  child.stdin.write(`${JSON.stringify(INIT_REQUEST)}\n`);
  // The SDK expects the initialized notification before it services requests.
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' })}\n`);
  for (const request of requests) {
    child.stdin.write(`${JSON.stringify(request)}\n`);
  }

  await settled;
  child.kill();

  const stdoutLines = completeLines(stdout.endsWith('\n') ? stdout : `${stdout}\n`);
  const responses = new Map<number, JsonRpcResponse>();
  for (const line of stdoutLines) {
    try {
      const parsed = JSON.parse(line) as JsonRpcResponse;
      if (typeof parsed.id === 'number') responses.set(parsed.id, parsed);
    } catch {
      // Left unparsed on purpose — the stdout-purity test asserts on this.
    }
  }

  return { stdoutLines, responses, stderr };
}

describe('stdio transport', () => {
  before(() => {
    assert.ok(
      existsSync(serverEntry),
      `Built server not found at ${serverEntry}. Run \`npm run build\` before testing.`
    );
  });

  test('writes nothing but JSON-RPC to stdout', async () => {
    // Arrange
    const listRequest = { jsonrpc: '2.0', id: 2, method: 'tools/list' };

    // Act
    const { stdoutLines } = await runServer([listRequest]);

    // Assert
    assert.ok(stdoutLines.length > 0, 'server produced no stdout at all');
    for (const line of stdoutLines) {
      let parsed: JsonRpcResponse;
      try {
        parsed = JSON.parse(line) as JsonRpcResponse;
      } catch {
        assert.fail(
          `Non-JSON line on stdout corrupts the JSON-RPC stream: ${JSON.stringify(line)}. ` +
          'A dependency is logging to stdout — stdout is reserved for the protocol, ' +
          'so route diagnostics to stderr (or silence the dependency, e.g. dotenv\'s `quiet: true`).'
        );
      }
      assert.equal(parsed!.jsonrpc, '2.0', `line is JSON but not JSON-RPC 2.0: ${line}`);
    }
  });

  test('diagnostics go to stderr, keeping stdout clean', async () => {
    // Arrange & Act
    const { stderr } = await runServer();

    // Assert
    assert.match(
      stderr,
      /running on stdio/,
      'expected the startup banner on stderr'
    );
  });

  test('completes the initialize handshake', async () => {
    // Arrange & Act
    const { responses } = await runServer();

    // Assert
    const init = responses.get(1);
    assert.ok(init, 'no response to initialize');
    assert.equal(init.error, undefined, `initialize failed: ${JSON.stringify(init.error)}`);
    assert.equal(init.result.protocolVersion !== undefined, true);
    assert.equal(init.result.serverInfo.name, 'wayd-mcp');
    assert.match(
      init.result.serverInfo.version,
      /^\d+\.\d+\.\d+/,
      'server version should be the semver from package.json'
    );
  });

  test('lists every registered tool with a valid schema', async () => {
    // Arrange
    const listRequest = { jsonrpc: '2.0', id: 2, method: 'tools/list' };

    // Act
    const { responses } = await runServer([listRequest]);

    // Assert
    const list = responses.get(2);
    assert.ok(list, 'no response to tools/list');
    assert.equal(list.error, undefined, `tools/list failed: ${JSON.stringify(list.error)}`);

    const { tools } = list.result as { tools: { name: string; description: string; inputSchema: any }[] };
    assert.ok(tools.length > 0, 'server exposed no tools');

    for (const tool of tools) {
      assert.ok(tool.name, 'tool is missing a name');
      assert.ok(tool.description, `tool ${tool.name} is missing a description`);
      assert.equal(tool.inputSchema?.type, 'object', `tool ${tool.name} has a non-object inputSchema`);
    }

    const names = tools.map(t => t.name);
    assert.equal(new Set(names).size, names.length, 'duplicate tool names would shadow each other');
  });

  test('advertises every status transition as destructive so clients confirm first', async () => {
    // Arrange
    // Status changes are published state other people act on, so a client must be
    // able to prompt before one runs. The hint travels in the tools/list payload,
    // so this asserts the wire contract rather than the local definitions.
    const listRequest = { jsonrpc: '2.0', id: 2, method: 'tools/list' };

    // Act
    const { responses } = await runServer([listRequest]);

    // Assert
    const list = responses.get(2);
    assert.ok(list, 'no response to tools/list');
    const { tools } = list.result as {
      tools: { name: string; annotations?: { readOnlyHint?: boolean; destructiveHint?: boolean } }[];
    };

    const transitions = tools.filter(t =>
      /_(Approve|Activate|Complete|Cancel|Close|Archive)$/.test(t.name)
    );
    assert.ok(transitions.length > 0, 'no status transition tools found — did they get renamed?');

    for (const tool of transitions) {
      assert.equal(
        tool.annotations?.destructiveHint,
        true,
        `${tool.name} changes a published status but is not marked destructive, so a client would run it without confirming`
      );
    }

    // A read must never be advertised as destructive. Tool names are prefixed by area
    // (`Portfolios_GetPortfolios`), so the verb sits after the underscore — matching on a
    // leading "Get" would silently pass without inspecting anything.
    const reads = tools.filter(t => /_(Get|List)/.test(t.name));
    assert.ok(reads.length > 0, 'no read tools matched — did the naming convention change?');

    const mislabelledReads = reads.filter(t => t.annotations?.destructiveHint);
    assert.deepEqual(
      mislabelledReads.map(t => t.name),
      [],
      'these read tools are marked destructive, so clients would confirm before a harmless read'
    );
  });

  test('reports unknown tools as errors instead of crashing', async () => {
    // Arrange
    const callRequest = {
      jsonrpc: '2.0',
      id: 2,
      method: 'tools/call',
      params: { name: 'Tool_That_Does_Not_Exist', arguments: {} },
    };

    // Act
    const { responses } = await runServer([callRequest]);

    // Assert
    const call = responses.get(2);
    assert.ok(call, 'no response to tools/call');
    assert.equal(call.result.isError, true, 'unknown tool should return isError');
    const [firstBlock] = call.result.content as { type: string; text: string }[];
    assert.ok(firstBlock, 'expected an error message in the result content');
    assert.match(firstBlock.text, /Unknown tool/i);
  });

  test('exits with a stderr message when base URL is unconfigured', async () => {
    // Arrange
    const envWithoutBaseUrl = { ...testEnv, WAYD_API_BASE_URL: '' };

    // Act
    const { stdoutLines, stderr } = await runServer([], { env: envWithoutBaseUrl });

    // Assert
    assert.match(stderr, /WAYD_API_BASE_URL/, 'expected a stderr message naming the missing setting');
    assert.equal(stdoutLines.length, 0, 'a config error must not write to the protocol stream');
  });
});
