/**
 * Minimal JSON Schema -> Zod 4 source-code emitter.
 *
 * Replaces the `json-schema-to-zod` package, whose repository was archived in
 * April 2026 and which still emits Zod 3 syntax (its `zodVersion` option never
 * reaches the string-format parser, so the output needed rewriting afterwards).
 *
 * This covers exactly the JSON Schema subset that NSwag produces for the Wayd
 * API — verified against all tool definitions: `type`, `properties`, `required`,
 * `items`, `enum`, `format`, `maxLength`, `pattern`, `description`, and union
 * types expressed as `type: [...]`. There is deliberately no support for `$ref`,
 * `allOf`/`anyOf`/`oneOf`, conditionals, or `additionalProperties`: none appear
 * in the input, and `assertSupported` fails the build if any ever do rather
 * than silently emitting a schema that validates the wrong thing.
 */

export interface JsonSchema {
    type?: string | string[];
    properties?: Record<string, JsonSchema>;
    required?: string[];
    items?: JsonSchema;
    enum?: unknown[];
    format?: string;
    maxLength?: number;
    minLength?: number;
    pattern?: string;
    description?: string;
    [key: string]: unknown;
}

/** JSON Schema keywords this emitter understands. Anything else is a build error. */
const SUPPORTED_KEYWORDS = new Set([
    'type',
    'properties',
    'required',
    'items',
    'enum',
    'format',
    'maxLength',
    'minLength',
    'pattern',
    'description',
]);

/** `format` values mapped to their Zod 4 constructors. */
const STRING_FORMATS: Record<string, string> = {
    uuid: 'z.uuid()',
    date: 'z.iso.date()',
    'date-time': 'z.iso.datetime({ offset: true })',
};

/**
 * Rejects any keyword this emitter does not implement. A schema using an
 * unsupported feature must fail loudly at build time — emitting a permissive
 * schema instead would let malformed tool arguments reach the API.
 */
function assertSupported(schema: JsonSchema, path: string): void {
    for (const keyword of Object.keys(schema)) {
        if (!SUPPORTED_KEYWORDS.has(keyword)) {
            throw new Error(
                `Unsupported JSON Schema keyword "${keyword}" at ${path}. ` +
                `Add support for it in scripts/json-schema-to-zod.ts — do not ignore it, ` +
                `since a silently dropped constraint weakens argument validation.`
            );
        }
    }
}

/** Emits a JS string literal, escaping via JSON so quotes and newlines survive. */
function literal(value: string): string {
    return JSON.stringify(value);
}

/** Builds the schema for a single, non-union type. */
function emitForType(type: string, schema: JsonSchema, path: string): string {
    switch (type) {
        case 'string': {
            const formatSchema = schema.format ? STRING_FORMATS[schema.format] : undefined;
            if (formatSchema) {
                // Format constructors are standalone schemas, so length and
                // pattern constraints (which JSON Schema allows alongside a
                // format) would have nowhere to attach.
                if (schema.maxLength !== undefined || schema.minLength !== undefined || schema.pattern) {
                    throw new Error(
                        `Cannot combine format "${schema.format}" with length/pattern constraints at ${path}.`
                    );
                }
                return formatSchema;
            }

            let result = 'z.string()';
            if (schema.minLength !== undefined) result += `.min(${schema.minLength})`;
            if (schema.maxLength !== undefined) result += `.max(${schema.maxLength})`;
            if (schema.pattern) result += `.regex(new RegExp(${literal(schema.pattern)}))`;
            return result;
        }

        case 'number':
        case 'integer':
            return 'z.number()';

        case 'boolean':
            return 'z.boolean()';

        case 'null':
            return 'z.null()';

        case 'array':
            return `z.array(${schema.items ? emit(schema.items, `${path}.items`) : 'z.any()'})`;

        case 'object': {
            const properties = schema.properties ?? {};
            const required = new Set(schema.required ?? []);
            const entries = Object.entries(properties).map(([key, value]) => {
                const child = emit(value, `${path}.${key}`);
                return `${literal(key)}: ${required.has(key) ? child : `${child}.optional()`}`;
            });
            return entries.length ? `z.object({ ${entries.join(', ')} })` : 'z.object({})';
        }

        default:
            throw new Error(`Unsupported JSON Schema type "${type}" at ${path}.`);
    }
}

/**
 * Converts a JSON Schema node into Zod 4 source code.
 *
 * @param schema The schema node to convert.
 * @param path   Dotted location used in error messages.
 */
export function emit(schema: JsonSchema, path = 'root'): string {
    assertSupported(schema, path);

    // `enum` fully determines the schema, so it is handled before `type`.
    if (schema.enum) {
        const values = schema.enum;
        const allStrings = values.every(v => typeof v === 'string');
        const base = allStrings
            ? `z.enum([${values.map(v => literal(v as string)).join(',')}])`
            : `z.union([${values.map(v => `z.literal(${JSON.stringify(v)})`).join(', ')}])`;
        return schema.description ? `${base}.describe(${literal(schema.description)})` : base;
    }

    const types = schema.type === undefined
        ? []
        : Array.isArray(schema.type) ? schema.type : [schema.type];

    let result: string;
    if (types.length === 0) {
        result = 'z.any()';
    } else if (types.length === 1) {
        result = emitForType(types[0]!, schema, path);
    } else {
        // A union: each member carries the description, matching how the
        // previous generator emitted `type: [...]` schemas.
        const members = types.map(type => {
            const member = emitForType(type, schema, path);
            return schema.description ? `${member}.describe(${literal(schema.description)})` : member;
        });
        result = `z.union([${members.join(', ')}])`;
    }

    return schema.description ? `${result}.describe(${literal(schema.description)})` : result;
}
