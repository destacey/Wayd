/**
 * Unit tests for the annotations each tool advertises. Clients decide from these whether to
 * confirm with the user before running a tool, so a wrong default is a write that runs unasked.
 */
import { test, describe } from 'node:test';
import assert from 'node:assert/strict';

import { annotationsFor } from '../build/annotations.js';
import { toolDefinitionMap } from '../build/tools/index.js';
import type { McpToolDefinition } from '../build/types.js';

function definition(method: string, annotations: McpToolDefinition['annotations']): McpToolDefinition {
  return {
    name: 'Test_Tool',
    description: 'test fixture',
    inputSchema: { type: 'object', properties: {} },
    method,
    pathTemplate: '/api/test',
    executionParameters: [],
    securityRequirements: [],
    annotations,
  };
}

describe('annotationsFor', () => {
  test('treats a write with only a title as destructive', async () => {
    // Arrange
    const post = definition('post', { title: 'Do something' });

    // Act
    const annotations = annotationsFor(post);

    // Assert
    assert.equal(annotations.destructiveHint, true, 'a write must be confirmed unless it says otherwise');
    assert.equal(annotations.readOnlyHint, false);
    assert.equal(annotations.idempotentHint, false, 'POST is not idempotent');
  });

  test('derives a read from GET and idempotence from PUT and DELETE', async () => {
    // Arrange
    const get = definition('get', { title: 'Read' });
    const put = definition('put', { title: 'Replace' });
    const del = definition('delete', { title: 'Remove' });

    // Act
    const read = annotationsFor(get);
    const replace = annotationsFor(put);
    const remove = annotationsFor(del);

    // Assert
    assert.deepEqual(
      { readOnlyHint: read.readOnlyHint, destructiveHint: read.destructiveHint },
      { readOnlyHint: true, destructiveHint: false }
    );
    assert.equal(replace.idempotentHint, true);
    assert.equal(replace.destructiveHint, true);
    assert.equal(remove.idempotentHint, true);
    assert.equal(remove.destructiveHint, true);
  });

  test("lets a definition's own annotations override its method's defaults", async () => {
    // Arrange
    const create = definition('post', { title: 'Create', destructiveHint: false });

    // Act
    const annotations = annotationsFor(create);

    // Assert
    assert.equal(annotations.destructiveHint, false);
    assert.equal(annotations.title, 'Create');
  });

  test('marks every registered tool closed-world, titled, and read-only only when it is a GET', async () => {
    // Arrange
    const definitions = [...toolDefinitionMap.values()];

    // Act
    const advertised = definitions.map(def => ({ def, annotations: annotationsFor(def) }));

    // Assert
    assert.deepEqual(
      advertised.filter(({ annotations }) => annotations.openWorldHint !== false).map(({ def }) => def.name),
      [],
      'every tool reaches only the Wayd API'
    );
    assert.deepEqual(
      advertised.filter(({ annotations }) => !annotations.title?.trim()).map(({ def }) => def.name),
      [],
      'clients show the title in place of the tool name'
    );
    assert.deepEqual(
      advertised
        .filter(({ def, annotations }) => annotations.readOnlyHint && def.method.toLowerCase() !== 'get')
        .map(({ def }) => def.name),
      [],
      'a tool that sends a write must never be advertised as read-only'
    );
  });

  test('lets a write skip confirmation only when it is named for adding something', async () => {
    // Arrange
    // Only a write that purely adds a record may opt out. The name is the one signal that
    // survives a copied annotation line, and listing the additive verbs rather than the
    // destructive ones means a new kind of change is confirmed until someone decides otherwise.
    const additive = /_(Create|Add|Plan|Assemble|Start|Preflight)/;
    const writes = [...toolDefinitionMap.values()].filter(def => def.method.toLowerCase() !== 'get');

    // Act
    const unconfirmed = writes.filter(def => !annotationsFor(def).destructiveHint);

    // Assert
    assert.ok(unconfirmed.length > 0, 'no write opts out — did the add-only annotations get lost?');
    assert.deepEqual(
      unconfirmed.filter(def => !additive.test(def.name)).map(def => def.name),
      [],
      'these writes skip confirmation but are not named for adding a record'
    );
  });
});
