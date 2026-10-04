/**
 * Keeps the snapshot the hosted MCP server (`/mcp` in Wayd.Web.Api) is tested against in step with this
 * package. Tool names are a contract with existing skills and prompts, and the hosted server must carry
 * each tool's title, annotations, arguments and the server instructions over unchanged, so the API's
 * integration tests compare its tool list with this snapshot.
 *
 * Run with UPDATE_PARITY_SNAPSHOT=1 to rewrite the snapshot after a deliberate change here.
 */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { annotationsFor } from '../build/annotations.js';
import { SERVER_INSTRUCTIONS } from '../build/instructions.js';
import { toolDefinitionMap } from '../build/tools/index.js';

const snapshotPath = join(
  dirname(fileURLToPath(import.meta.url)),
  '../../../tests/Wayd.Web.Api.IntegrationTests/Mcp/npm-tool-parity.json'
);

function currentSnapshot() {
  const tools = [...toolDefinitionMap.values()]
    .sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0))
    .map(definition => {
      const { title, readOnlyHint, destructiveHint, idempotentHint, openWorldHint } = annotationsFor(definition);
      return {
        name: definition.name,
        title,
        annotations: { readOnlyHint, destructiveHint, idempotentHint, openWorldHint },
        arguments: Object.keys(definition.inputSchema?.properties ?? {}).sort(),
        required: [...(definition.inputSchema?.required ?? [])].sort(),
      };
    });
  return { instructions: SERVER_INSTRUCTIONS, tools };
}

test('the hosted-server parity snapshot matches this package', () => {
  // Arrange
  const current = currentSnapshot();
  if (process.env.UPDATE_PARITY_SNAPSHOT === '1') {
    writeFileSync(snapshotPath, `${JSON.stringify(current, null, 2)}\n`);
  }

  // Act
  const snapshot = JSON.parse(readFileSync(snapshotPath, 'utf8'));

  // Assert
  assert.deepEqual(snapshot, current, 'Run the tests with UPDATE_PARITY_SNAPSHOT=1 and carry the change over to the API.');
});
