/**
 * The hosted MCP server's catalogue, as the evaluation's stub advertises it: every tool's name, title,
 * description, input schema and annotations, and the server instructions.
 *
 * Fetched once from a running Wayd API (`npm run catalog`) and saved to `catalog.json`, which is not
 * committed: the tool list follows the API, and every run in a comparison should use the same one. Run as a
 * script, this module fetches it:
 *
 *     WAYD_API_KEY=<token> npm run catalog -- https://localhost:<port>/mcp
 */
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';
import type { Tool } from '@modelcontextprotocol/sdk/types.js';

const evalsDir = dirname(fileURLToPath(import.meta.url));

/** Where the catalogue is read from: `WAYD_EVAL_CATALOG`, or `catalog.json` beside this file. */
export const catalogPath = process.env.WAYD_EVAL_CATALOG ?? join(evalsDir, 'catalog.json');

/** What the stub advertises, as the hosted server sent it. */
export interface Catalog {
  /** The `/mcp` address it was fetched from. */
  source: string;
  fetchedAt: string;
  server: { name: string; version: string };
  instructions: string;
  tools: Tool[];
}

/** The saved catalogue; throws, saying how to fetch one, when there is none. */
export function loadCatalog(): Catalog {
  if (!existsSync(catalogPath)) {
    throw new Error(`no MCP catalogue at ${catalogPath}. Start the API and fetch one: WAYD_API_KEY=<token> npm run catalog -- https://localhost:<port>/mcp`);
  }
  return JSON.parse(readFileSync(catalogPath, 'utf8')) as Catalog;
}

/** Every tool on `url`, with every toolset and the server's instructions, read with the token `apiKey`. */
export async function fetchCatalog(url: string, apiKey: string): Promise<Catalog> {
  const client = new Client({ name: 'wayd-skill-evals', version: '1.0.0' });
  await client.connect(new StreamableHTTPClientTransport(new URL(url), { requestInit: { headers: { 'x-api-key': apiKey } } }));
  try {
    const tools: Tool[] = [];
    let cursor: string | undefined;
    do {
      const page = await client.listTools(cursor ? { cursor } : {});
      tools.push(...page.tools);
      cursor = page.nextCursor;
    } while (cursor);
    const server = client.getServerVersion();
    return {
      source: url,
      fetchedAt: new Date().toISOString(),
      server: { name: server?.name ?? 'wayd', version: server?.version ?? '' },
      instructions: client.getInstructions() ?? '',
      tools,
    };
  } finally {
    await client.close();
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const url = process.argv[2];
  const apiKey = process.env.WAYD_API_KEY;
  if (!url || !apiKey) {
    console.error('usage: WAYD_API_KEY=<token> npm run catalog -- https://<your-wayd-api>/mcp');
    process.exit(1);
  }
  const catalog = await fetchCatalog(url, apiKey);
  writeFileSync(catalogPath, JSON.stringify(catalog, null, 2) + '\n');
  console.error(`${catalog.tools.length} tools from ${url} (${catalog.server.name} ${catalog.server.version}) written to ${catalogPath}`);
}
