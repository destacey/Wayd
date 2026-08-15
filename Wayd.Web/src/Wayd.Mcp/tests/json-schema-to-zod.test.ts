/**
 * Unit tests for the in-house JSON Schema -> Zod 4 emitter.
 *
 * This replaced the archived `json-schema-to-zod` package. The emitter covers
 * only the subset NSwag actually produces, so the guard tests matter as much as
 * the happy path: an unsupported keyword must fail the build rather than be
 * silently dropped, since a dropped constraint weakens argument validation.
 */
import { test, describe } from 'node:test';
import assert from 'node:assert/strict';

import { emit, type JsonSchema } from '../scripts/json-schema-to-zod.js';

describe('emit', () => {
  test('maps string formats to Zod 4 top-level validators', () => {
    // Arrange & Act & Assert
    assert.equal(emit({ type: 'string', format: 'uuid' }), 'z.uuid()');
    assert.equal(emit({ type: 'string', format: 'date' }), 'z.iso.date()');
    assert.equal(
      emit({ type: 'string', format: 'date-time' }),
      'z.iso.datetime({ offset: true })'
    );
  });

  test('ignores formats that carry no validation meaning', () => {
    // Arrange & Act — int32/decimal describe numeric width, not string shape
    const result = emit({ type: 'string', format: 'int32' });

    // Assert
    assert.equal(result, 'z.string()');
  });

  test('applies string length and pattern constraints', () => {
    // Arrange & Act & Assert
    assert.equal(emit({ type: 'string', maxLength: 10 }), 'z.string().max(10)');
    assert.equal(emit({ type: 'string', minLength: 2 }), 'z.string().min(2)');
    assert.equal(
      emit({ type: 'string', pattern: '^a$' }),
      'z.string().regex(new RegExp("^a$"))'
    );
  });

  test('maps scalar and array types', () => {
    // Arrange & Act & Assert
    assert.equal(emit({ type: 'number' }), 'z.number()');
    assert.equal(emit({ type: 'integer' }), 'z.number()');
    assert.equal(emit({ type: 'boolean' }), 'z.boolean()');
    assert.equal(emit({ type: 'null' }), 'z.null()');
    assert.equal(emit({ type: 'array', items: { type: 'number' } }), 'z.array(z.number())');
  });

  test('expresses multi-type schemas as a union', () => {
    // Arrange & Act
    const result = emit({ type: ['string', 'null'] });

    // Assert
    assert.equal(result, 'z.union([z.string(), z.null()])');
  });

  test('marks properties optional unless listed as required', () => {
    // Arrange
    const schema: JsonSchema = {
      type: 'object',
      properties: { a: { type: 'string' }, b: { type: 'string' } },
      required: ['a'],
    };

    // Act
    const result = emit(schema);

    // Assert
    assert.equal(result, 'z.object({ "a": z.string(), "b": z.string().optional() })');
  });

  test('emits an empty object schema when there are no properties', () => {
    // Arrange & Act & Assert
    assert.equal(emit({ type: 'object', properties: {} }), 'z.object({})');
  });

  test('carries descriptions through as .describe()', () => {
    // Arrange & Act
    const result = emit({ type: 'string', description: 'a "quoted" note' });

    // Assert — JSON escaping keeps embedded quotes valid in the emitted source
    assert.equal(result, 'z.string().describe("a \\"quoted\\" note")');
  });

  test('emits string enums as z.enum', () => {
    // Arrange & Act
    const result = emit({ type: 'string', enum: ['Healthy', 'AtRisk'] });

    // Assert
    assert.equal(result, 'z.enum(["Healthy","AtRisk"])');
  });

  test('nests object schemas', () => {
    // Arrange
    const schema: JsonSchema = {
      type: 'object',
      properties: {
        body: { type: 'object', properties: { id: { type: 'string', format: 'uuid' } }, required: ['id'] },
      },
      required: ['body'],
    };

    // Act
    const result = emit(schema);

    // Assert
    assert.equal(result, 'z.object({ "body": z.object({ "id": z.uuid() }) })');
  });

  describe('guards', () => {
    const unsupported: [string, JsonSchema][] = [
      ['$ref', { $ref: '#/definitions/X' } as JsonSchema],
      ['allOf', { allOf: [{ type: 'string' }] } as JsonSchema],
      ['anyOf', { anyOf: [{ type: 'string' }] } as JsonSchema],
      ['oneOf', { oneOf: [{ type: 'string' }] } as JsonSchema],
      ['additionalProperties', { type: 'object', additionalProperties: false } as JsonSchema],
    ];

    for (const [keyword, schema] of unsupported) {
      test(`rejects the unsupported keyword ${keyword}`, () => {
        // Arrange & Act & Assert — silently ignoring it would weaken validation
        assert.throws(() => emit(schema), new RegExp(`Unsupported JSON Schema keyword "\\${keyword}"`));
      });
    }

    test('rejects an unknown type', () => {
      // Arrange & Act & Assert
      assert.throws(() => emit({ type: 'bogus' }), /Unsupported JSON Schema type "bogus"/);
    });

    test('rejects a format combined with length or pattern constraints', () => {
      // Arrange & Act & Assert — the format constructor has nowhere to chain them
      assert.throws(
        () => emit({ type: 'string', format: 'uuid', maxLength: 5 }),
        /Cannot combine format/
      );
    });

    test('names the offending path so a build failure is actionable', () => {
      // Arrange
      const schema: JsonSchema = {
        type: 'object',
        properties: { outer: { type: 'object', properties: { inner: { $ref: '#/x' } as JsonSchema } } },
      };

      // Act & Assert
      assert.throws(() => emit(schema, 'MyTool'), /MyTool\.outer\.inner/);
    });
  });
});
