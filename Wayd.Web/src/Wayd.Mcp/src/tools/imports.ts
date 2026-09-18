import type { McpToolDefinition } from '../types.js';

/**
 * Tracking and control for import runs, whatever kind of file each one was.
 *
 * Every submitted file becomes a run the API reports on here, so these tools
 * serve a run submitted from Settings → Imports as well as one this server sent.
 */

const readsOnly = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
} as const;

const RUN_ID = { type: 'string', format: 'uuid', description: 'Import run ID (UUID).' };

const ATOMICITY =
  '`atomicity` (PerRow; Atomic, where one rejected row means nothing is written; or PerGroup, where the rows sharing a group — ' +
  'named by `groupNoun`, such as every dependency of one product — apply together or not at all and the other groups are kept)';

const RUN_SHAPE =
  'A run carries `status` (Queued, Processing, Cancelling, Succeeded, PartiallySucceeded, Failed, Cancelled), `isTerminal`, ' +
  `\`isPreflight\`, ${ATOMICITY}, and the counts \`totalRowCount\`, ` +
  '`succeededRowCount`, `failedRowCount` and `unappliedRowCount`. `canManage` says whether you may cancel, resume, retry or apply it.';

const SUBMITTED_RUN =
  'Answers with the new run once it has finished, or while it is still queued or running if it takes longer than a few seconds — ' +
  'check `isTerminal`, and poll `Imports_GetById` until it is true.';

export const definitions: [string, McpToolDefinition][] = [

  ['Imports_GetDefinitions', {
    name: 'Imports_GetDefinitions',
    description: `List the kinds of import you may see. Each has its \`key\` (what \`Imports_GetList\` filters on), \`displayName\`, ${ATOMICITY}, \`maxRows\` and \`preflightMaxRows\` (the most rows one file may hold for each), and \`canSubmit\`, whether you may submit that kind of file and act on its runs.`,
    inputSchema: { type: 'object', properties: {} },
    method: 'get',
    pathTemplate: '/api/imports/definitions',
    executionParameters: [],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'List import types', ...readsOnly },
  }],

  ['Imports_GetList', {
    name: 'Imports_GetList',
    description: `List import runs, newest first, as \`processes\` with a \`totalCount\`. Only runs of the kinds you may submit are included, or every kind if you hold View Imports. ${RUN_SHAPE}`,
    inputSchema: {
      type: 'object',
      properties: {
        status: { type: 'string', enum: ['Queued', 'Processing', 'Cancelling', 'Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled'], description: 'Only runs in this status.' },
        importType: { type: 'string', description: 'Only runs of this kind, by its key from `Imports_GetDefinitions` (e.g. `ppm.projects`).' },
        submittedByUserId: { type: 'string', description: 'Only runs submitted by this user.' },
        submissionGroupId: { type: 'string', format: 'uuid', description: 'Only runs submitted together under this group id.' },
        pageNumber: { type: 'integer', description: 'Page number, starting at 1. Defaults to 1.' },
        pageSize: { type: 'integer', description: 'Runs per page. Defaults to 100; the API returns at most 500.' },
      },
    },
    method: 'get',
    pathTemplate: '/api/imports',
    executionParameters: [
      { name: 'status', in: 'query' },
      { name: 'importType', in: 'query' },
      { name: 'submittedByUserId', in: 'query' },
      { name: 'submissionGroupId', in: 'query' },
      { name: 'pageNumber', in: 'query' },
      { name: 'pageSize', in: 'query' },
    ],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'List import runs', ...readsOnly },
  }],

  ['Imports_GetById', {
    name: 'Imports_GetById',
    description: `Get one import run's status and counts. Poll this until \`isTerminal\` is true to follow a run that was still going when it was submitted. ${RUN_SHAPE} \`error\` explains a run that could not continue; a rejected row's reason is on the row, from \`Imports_GetRows\`.`,
    inputSchema: { type: 'object', properties: { id: RUN_ID }, required: ['id'] },
    method: 'get',
    pathTemplate: '/api/imports/{id}',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Get import run', ...readsOnly },
  }],

  ['Imports_GetRows', {
    name: 'Imports_GetRows',
    description: 'Get a page of an import run\'s row outcomes, as `rows` with a `totalCount`. Each row has its `importId` (the file\'s ImportId column, or its position when the file had none), `rowNumber`, `status` (Pending, Succeeded, Failed, Cancelled), the `error` a rejected row was refused for, any `warning` a successful row recorded, and `createdEntityId`. In a preflight, Succeeded means the row would have been imported. Filter by `status: Failed` to see only what needs fixing.',
    inputSchema: {
      type: 'object',
      properties: {
        id: RUN_ID,
        status: { type: 'string', enum: ['Pending', 'Succeeded', 'Failed', 'Cancelled'], description: 'Only rows with this outcome.' },
        pageNumber: { type: 'integer', description: 'Page number, starting at 1. Defaults to 1.' },
        pageSize: { type: 'integer', description: 'Rows per page. Defaults to 50; the API returns at most 500.' },
      },
      required: ['id'],
    },
    method: 'get',
    pathTemplate: '/api/imports/{id}/rows',
    executionParameters: [{ name: 'id', in: 'path' }, { name: 'status', in: 'query' }, { name: 'pageNumber', in: 'query' }, { name: 'pageSize', in: 'query' }],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Get import run rows', ...readsOnly },
  }],

  ['Imports_Cancel', {
    name: 'Imports_Cancel',
    description: 'Stop an import run that is still going. A request, not an undo: the worker stops at its next batch boundary, and rows already applied stay applied. The rest can be picked up later with `Imports_Resume`. Refused for a run that has already finished.',
    inputSchema: { type: 'object', properties: { id: RUN_ID }, required: ['id'] },
    method: 'post',
    pathTemplate: '/api/imports/{id}/cancel',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Stop import run', readOnlyHint: false, destructiveHint: true, idempotentHint: false },
  }],

  ['Imports_Resume', {
    name: 'Imports_Resume',
    description: 'Queue a finished import run again to apply the rows it never reached — after it was stopped, or failed partway. Rows that succeeded are never reapplied, and rejected rows stay rejected (use `Imports_RetryFailed` for those). Answers with `queuedRowCount` and `skippedRowCount`, the rows whose data the 30-day retention window already discarded. Refused for a run still going, and for a preflight, which is imported with `Imports_Apply` instead.',
    inputSchema: { type: 'object', properties: { id: RUN_ID }, required: ['id'] },
    method: 'post',
    pathTemplate: '/api/imports/{id}/resume',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Resume import run', readOnlyHint: false, destructiveHint: true, idempotentHint: false },
  }],

  ['Imports_RetryFailed', {
    name: 'Imports_RetryFailed',
    description: 'Queue a finished import run again, reattempting its rejected rows as well as any it never reached. Fix whatever the rows were rejected for first — the rows are resubmitted exactly as they were, so a problem in the file itself needs a corrected file instead. Rows that succeeded are never reapplied. Answers with `queuedRowCount` and `skippedRowCount`. Refused for a run still going, and for a preflight.',
    inputSchema: { type: 'object', properties: { id: RUN_ID }, required: ['id'] },
    method: 'post',
    pathTemplate: '/api/imports/{id}/retry-failed',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Retry rejected import rows', readOnlyHint: false, destructiveHint: true, idempotentHint: false },
  }],

  ['Imports_Apply', {
    name: 'Imports_Apply',
    description: `Import, for real, the rows a finished preflight checked — without sending the file again. Every row is submitted, including ones the preflight rejected, and each is checked again against the data as it is now, so the import may still refuse a row the preflight passed. Applying the same preflight twice rejects the records the first import created as duplicates, or, for an import with no natural key such as deployments, creates them a second time; check \`appliedImportProcessId\` on the preflight first. Refused once the preflight's rows pass the 30-day retention window. ${SUBMITTED_RUN}`,
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string', format: 'uuid', description: 'ID (UUID) of the finished preflight run to import.' },
        submissionGroupId: { type: 'string', format: 'uuid', description: 'Optional group id recorded on the new run, to show it with other files submitted together.' },
      },
      required: ['id'],
    },
    method: 'post',
    pathTemplate: '/api/imports/{id}/apply',
    executionParameters: [{ name: 'id', in: 'path' }, { name: 'submissionGroupId', in: 'query' }],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Import a checked file', readOnlyHint: false, destructiveHint: true, idempotentHint: false },
  }],

];
