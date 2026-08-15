/**
 * Unit tests for error formatting and argument validation.
 *
 * The validation tests are deliberately behavioural rather than
 * implementation-shaped: they pin what a *caller* observes when arguments are
 * wrong. That makes them the safety net for a future zod 3 -> 4 upgrade, which
 * renames `ZodError.errors` to `.issues` and deprecates several of the string
 * validators the schema generator emits.
 */
import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import type { AxiosError } from 'axios';

import { formatApiError, executeApiTool, securitySchemes } from '../build/executor.js';
import { zodSchemas } from '../build/generated/zod-schemas.js';
import { toolDefinitionMap } from '../build/tools/index.js';
import type { McpToolDefinition } from '../build/types.js';

/** Minimal AxiosError shaped for formatApiError's three branches. */
function axiosErrorWith(parts: Partial<AxiosError>): AxiosError {
  return { isAxiosError: true, name: 'AxiosError', message: '', ...parts } as AxiosError;
}

describe('formatApiError', () => {
  test('summarises a JSON error response', () => {
    // Arrange
    const error = axiosErrorWith({
      response: {
        status: 404,
        statusText: 'Not Found',
        data: { title: 'Portfolio not found' },
        headers: {},
        config: {} as any,
      },
    });

    // Act
    const message = formatApiError(error);

    // Assert
    assert.match(message, /Status 404/);
    assert.match(message, /Not Found/);
    assert.match(message, /Portfolio not found/);
  });

  test('truncates long response bodies', () => {
    // Arrange
    const error = axiosErrorWith({
      response: {
        status: 500,
        statusText: 'Internal Server Error',
        data: 'x'.repeat(500),
        headers: {},
        config: {} as any,
      },
    });

    // Act
    const message = formatApiError(error);

    // Assert
    assert.match(message, /\.\.\./, 'expected an ellipsis marking truncation');
    assert.ok(message.length < 400, `message was not truncated (${message.length} chars)`);
  });

  test('describes a network error with no response', () => {
    // Arrange
    const error = axiosErrorWith({ request: {}, code: 'ECONNREFUSED' });

    // Act
    const message = formatApiError(error);

    // Assert
    assert.match(message, /Network Error/);
    assert.match(message, /ECONNREFUSED/);
  });

  test('falls back to the setup error message', () => {
    // Arrange
    const error = axiosErrorWith({ message: 'Invalid URL' });

    // Act
    const message = formatApiError(error);

    // Assert
    assert.match(message, /Invalid URL/);
  });
});

describe('generated zod schemas', () => {
  test('cover every registered tool', () => {
    // Arrange
    const toolNames = [...toolDefinitionMap.keys()];

    // Act
    const missing = toolNames.filter(name => !zodSchemas.has(name));

    // Assert
    assert.ok(toolNames.length > 0, 'no tools registered — the barrel export is empty');
    assert.deepEqual(missing, [], `tools without a generated schema: ${missing.join(', ')}`);
  });

  test('accept valid arguments and reject invalid ones', () => {
    // Arrange
    const schema = zodSchemas.get('Portfolios_GetPortfolio');
    assert.ok(schema, 'expected a schema for Portfolios_GetPortfolio');

    // Act
    const valid = schema.safeParse({ idOrKey: 'PORT-1' });
    const missingRequired = schema.safeParse({});
    const wrongType = schema.safeParse({ idOrKey: 42 });

    // Assert
    assert.equal(valid.success, true, 'a valid argument set was rejected');
    assert.equal(missingRequired.success, false, 'a missing required field was accepted');
    assert.equal(wrongType.success, false, 'a wrong-typed field was accepted');
  });

  test('use Zod 4 top-level format validators, not deprecated string methods', () => {
    // Arrange
    // Deprecated Zod 3 forms still work, so nothing else would fail if the
    // emitter regressed onto them — this asserts the generated output stays on
    // the current APIs.
    const generated = readFileSync(
      join(dirname(fileURLToPath(import.meta.url)), '../src/generated/zod-schemas.ts'),
      'utf8'
    );

    // Act
    const deprecated = [
      ...(generated.match(/z\.string\(\)\.(uuid|date|datetime|email|url|ipv4|ipv6|base64|ulid|nanoid|jwt|emoji|e164)\(/g) ?? []),
      // Deprecated aliases from Zod 4's compat shim ("Use z.ZodType instead").
      ...(generated.match(/z\.(ZodTypeAny|ZodSchema)\b/g) ?? []),
    ];

    // Assert
    assert.deepEqual(
      [...new Set(deprecated)],
      [],
      'generated schemas use deprecated Zod 3 APIs — check scripts/json-schema-to-zod.ts'
    );
  });

  test('validate uuid-formatted parameters', () => {
    // Arrange
    const schema = zodSchemas.get('Projects_GetWorkItems');
    assert.ok(schema, 'expected a schema for Projects_GetWorkItems');

    // Act
    const valid = schema.safeParse({ id: '3f2504e0-4f89-11d3-9a0c-0305e82c3301' });
    const notAUuid = schema.safeParse({ id: 'not-a-uuid' });

    // Assert
    assert.equal(valid.success, true, 'a well-formed uuid was rejected');
    assert.equal(notAUuid.success, false, 'a malformed uuid was accepted');
  });
});

describe('executeApiTool', () => {
  /** A tool definition pointing at an unroutable host, so nothing leaves the machine. */
  const unreachableTool: McpToolDefinition = {
    name: 'Test_Unreachable',
    description: 'test fixture',
    inputSchema: { type: 'object', properties: {} },
    method: 'get',
    pathTemplate: '/api/test',
    executionParameters: [],
    securityRequirements: [],
  };

  test('returns an error result instead of throwing when arguments are invalid', async () => {
    // Arrange
    const definition = toolDefinitionMap.get('Portfolios_GetPortfolio');
    assert.ok(definition, 'expected the Portfolios_GetPortfolio definition');

    // Act — idOrKey is required, so validation fails before any network call
    const result = await executeApiTool(
      'Portfolios_GetPortfolio',
      definition,
      {},
      securitySchemes
    );

    // Assert
    assert.equal(result.isError, true, 'invalid arguments should produce isError');
    const [firstBlock] = result.content;
    assert.ok(firstBlock, 'expected a validation message in the result content');
    assert.match(
      (firstBlock as { text: string }).text,
      /Invalid arguments/,
      'the message should tell the model its arguments were wrong'
    );
  });

  test('names the offending field and reason in the validation message', async () => {
    // Arrange
    // Guards the zod 3 -> 4 rename of `ZodError.errors` to `.issues`: reading the
    // removed property throws a TypeError, which would replace this actionable
    // message with an opaque crash. Asserting on the text catches that directly.
    const definition = toolDefinitionMap.get('Projects_GetWorkItems');
    assert.ok(definition, 'expected the Projects_GetWorkItems definition');

    // Act — `id` must be a uuid, so this fails format validation
    const result = await executeApiTool(
      'Projects_GetWorkItems',
      definition,
      { id: 'not-a-uuid' },
      securitySchemes
    );

    // Assert
    assert.equal(result.isError, true);
    const [firstBlock] = result.content;
    assert.ok(firstBlock, 'expected a validation message in the result content');
    const text = (firstBlock as { text: string }).text;
    assert.match(text, /id/, 'the message should name the offending field');
    assert.doesNotMatch(
      text,
      /undefined|\[object|TypeError/,
      'the message should be readable, not a leaked internal error'
    );
  });

  test('surfaces a network failure as an error result', async () => {
    // Arrange & Act
    const result = await executeApiTool(
      'Test_Unreachable',
      unreachableTool,
      {},
      securitySchemes
    );

    // Assert
    assert.equal(result.isError, true, 'a failed request should produce isError');
    const [firstBlock] = result.content;
    assert.ok(firstBlock, 'expected an error message in the result content');
    assert.equal(firstBlock.type, 'text');
  });
});
