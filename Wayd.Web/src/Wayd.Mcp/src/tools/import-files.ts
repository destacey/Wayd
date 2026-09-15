import type { McpToolDefinition } from '../types.js';
import { importFormats } from '../generated/import-formats.js';

/**
 * Checking a CSV file against Wayd without importing it.
 *
 * There is deliberately no tool that submits a file for real. A file only
 * reaches the import through `Imports_Apply` on a finished preflight, so an
 * agent always sees every row's outcome before anything is written.
 */

const importKeys = Object.keys(importFormats);

const IMPORT_TYPE = {
  type: 'string',
  enum: importKeys,
  description: 'The kind of import, by its key. `Imports_GetDefinitions` says which you may submit.',
};

/** Every file an import takes beyond its main one, described by the imports that take it. */
const extraFiles = new Map<string, string[]>();
for (const [key, format] of Object.entries(importFormats)) {
  for (const file of format.files) {
    if (file.field === 'file') continue;
    const uses = extraFiles.get(file.field) ?? [];
    uses.push(`${file.label ?? file.field} for \`${key}\` (${file.required ? 'required' : 'optional'})`);
    extraFiles.set(file.field, uses);
  }
}

const extraFileProperties = Object.fromEntries(
  [...extraFiles].map(([field, uses]) => [field, {
    type: 'string',
    description: `CSV text of the second file some imports take: ${uses.join('; ')}. Ignored by every other import.`,
  }])
);

export const definitions: [string, McpToolDefinition][] = [

  ['Imports_GetFileFormat', {
    name: 'Imports_GetFileFormat',
    description: 'Get what a kind of import\'s CSV files must contain, before writing one: each file it takes and whether it is required, and every column with its exact `header` spelling, whether a row must fill it, its type (text, id, date, timestamp, integer, number, boolean), and the only values it accepts where it names one of a fixed set. Every file needs the full header row even where a column is optional. Answered from the API description this server was built with, without calling Wayd.',
    inputSchema: { type: 'object', properties: { importType: IMPORT_TYPE }, required: ['importType'] },
    method: 'get',
    pathTemplate: '',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [],
    annotations: { title: 'Get import file format', readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
    localHandler: ({ importType }) => {
      const format = importFormats[String(importType)]!;
      const answer = {
        importType,
        description: format.description,
        files: format.files.map(file => ({
          argument: file.field,
          label: file.label,
          required: file.required,
          header: file.columns.map(column => column.name).join(','),
          columns: file.columns,
        })),
      };
      return { content: [{ type: 'text', text: JSON.stringify(answer, null, 2) }] };
    },
  }],

  ['Imports_Preflight', {
    name: 'Imports_Preflight',
    description: 'Check a CSV file against Wayd without importing it. Every row goes through the checks a real import runs, in the same order, against the data as it stands, and nothing is created. Answers with the preflight run once it has finished, or while still running if it takes longer than a few seconds — poll `Imports_GetById` until `isTerminal`. Then read `Imports_GetRows`: Succeeded rows would be imported, Failed rows carry the reason they would be refused, and every rejection is reported at once, even for an all-or-nothing import. To import the checked rows, call `Imports_Apply` with the preflight\'s id. A file with a column or cell problem is refused outright (400/422) and becomes no run. Call `Imports_GetFileFormat` first for the columns. A preflight holds at most `preflightMaxRows` rows (from `Imports_GetDefinitions`); split a larger file.',
    inputSchema: {
      type: 'object',
      properties: {
        importType: IMPORT_TYPE,
        file: { type: 'string', description: 'CSV text of the main file, header row first.' },
        ...extraFileProperties,
        submissionGroupId: { type: 'string', format: 'uuid', description: 'Optional group id of your choosing, recorded on the run, to show several files as one batch.' },
      },
      required: ['importType', 'file'],
    },
    method: 'post',
    pathTemplate: '',
    pathSelector: {
      parameter: 'importType',
      paths: Object.fromEntries(Object.entries(importFormats).map(([key, format]) => [key, format.path])),
    },
    fixedQuery: { validateOnly: true },
    executionParameters: [
      { name: 'file', in: 'formFile' },
      ...[...extraFiles.keys()].map(name => ({ name, in: 'formFile' })),
      { name: 'submissionGroupId', in: 'query' },
    ],
    requestBodyContentType: 'multipart/form-data',
    securityRequirements: [{ ApiKey: [] }],
    // Writes a run record and nothing else, so it needs no confirmation; applying it is what asks first.
    annotations: { title: 'Check an import file', readOnlyHint: false, destructiveHint: false, idempotentHint: false },
  }],

];
