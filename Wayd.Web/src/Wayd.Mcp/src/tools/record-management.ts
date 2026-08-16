import type { McpToolDefinition } from '../types.js';

/**
 * Create and update tools for portfolios, programs, and projects.
 *
 * Updates here are whole-record PUTs, not patches: every field is written from the
 * request body, so an omitted field is not "leave it alone" — it is "set it to
 * nothing". That is why every update tool below insists on reading the record first
 * and echoing back the fields that should not change.
 */

/** Marks a tool that changes a shared record a client should confirm before running. */
const requiresConfirmation = {
  destructiveHint: true,
  readOnlyHint: false,
  idempotentHint: false,
} as const;

/**
 * The role-list trap, stated on every tool that accepts one. Role lists replace the
 * whole assignment set for that role, and a list that is omitted or empty removes
 * every existing assignment rather than leaving it untouched.
 */
const ROLE_LISTS =
  'Role lists REPLACE the existing assignments for that role — they do not add to them. ' +
  'An omitted or empty list REMOVES everyone currently holding that role. ' +
  'Always read the current record first and pass back the full membership you intend to keep, including people you are not changing.';

const READ_FIRST =
  'This is a whole-record update, not a patch: every field is overwritten from the request body, so omitting a field clears it. ' +
  'Read the record first and echo back every value that should stay the same.';

const LEADERSHIP =
  'Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough.';

const CONFIRM = 'Changes a record other people rely on, so confirm with the user before calling.';

/** Role id lists shared by portfolios, programs, and projects. */
const roleProps = (includeMembers: boolean) => ({
  sponsorIds: { type: ['array', 'null'], items: { type: 'string', format: 'uuid' }, description: `Employee IDs. ${ROLE_LISTS}` },
  ownerIds: { type: ['array', 'null'], items: { type: 'string', format: 'uuid' }, description: `Employee IDs. ${ROLE_LISTS}` },
  managerIds: { type: ['array', 'null'], items: { type: 'string', format: 'uuid' }, description: `Employee IDs. ${ROLE_LISTS}` },
  ...(includeMembers
    ? { memberIds: { type: ['array', 'null'], items: { type: 'string', format: 'uuid' }, description: `Employee IDs. ${ROLE_LISTS}` } }
    : {}),
});

const strategicThemeIds = {
  strategicThemeIds: { type: ['array', 'null'], items: { type: 'string', format: 'uuid' }, description: 'Strategic theme IDs. Replaces the existing set.' },
};

const dateProps = {
  start: { type: ['string', 'null'], format: 'date', description: 'ISO date (YYYY-MM-DD). Start and end must both be set or both omitted.' },
  end: { type: ['string', 'null'], format: 'date', description: 'ISO date (YYYY-MM-DD). Must be on or after start.' },
};

export const definitions: [string, McpToolDefinition][] = [

  // ----------------------------------------------------------------- Portfolios

  ['Portfolios_Create', {
    name: 'Portfolios_Create',
    description: `Create a portfolio. It starts in Proposed status — use Portfolios_Activate to make it active, which also stamps its start date. ${ROLE_LISTS} ${CONFIRM}`,
    inputSchema: {
      type: 'object',
      properties: {
        requestBody: {
          type: 'object',
          properties: {
            name: { type: 'string', maxLength: 128 },
            description: { type: 'string', maxLength: 1024 },
            ...roleProps(false),
          },
          required: ['name', 'description'],
        },
      },
      required: ['requestBody'],
    },
    method: 'post',
    pathTemplate: '/api/ppm/portfolios',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Create portfolio', destructiveHint: false, readOnlyHint: false, idempotentHint: false },
  }],

  ['Portfolios_Update', {
    name: 'Portfolios_Update',
    description: `Update a portfolio's name, description, and role assignments. ${READ_FIRST} ${ROLE_LISTS} The id in the body must match the id path parameter. ${LEADERSHIP} ${CONFIRM}`,
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string', format: 'uuid', description: 'Portfolio ID (UUID only, not a key).' },
        requestBody: {
          type: 'object',
          properties: {
            id: { type: 'string', format: 'uuid', description: 'Must match the id path parameter.' },
            name: { type: 'string', maxLength: 128 },
            description: { type: 'string', maxLength: 1024 },
            ...roleProps(false),
          },
          required: ['id', 'name', 'description'],
        },
      },
      required: ['id', 'requestBody'],
    },
    method: 'put',
    pathTemplate: '/api/ppm/portfolios/{id}',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: 'application/json',
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Update portfolio', ...requiresConfirmation },
  }],

  // ------------------------------------------------------------------- Programs

  ['Programs_Create', {
    name: 'Programs_Create',
    description: `Create a program inside a portfolio. It starts in Proposed status — use Programs_Activate to make it active, which requires a start and end date. ${ROLE_LISTS} ${CONFIRM}`,
    inputSchema: {
      type: 'object',
      properties: {
        requestBody: {
          type: 'object',
          properties: {
            name: { type: 'string', maxLength: 128 },
            description: { type: 'string', maxLength: 2048 },
            portfolioId: { type: 'string', format: 'uuid', description: 'The portfolio this program belongs to.' },
            ...dateProps,
            ...roleProps(false),
            ...strategicThemeIds,
          },
          required: ['name', 'description', 'portfolioId'],
        },
      },
      required: ['requestBody'],
    },
    method: 'post',
    pathTemplate: '/api/ppm/programs',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Create program', destructiveHint: false, readOnlyHint: false, idempotentHint: false },
  }],

  ['Programs_Update', {
    name: 'Programs_Update',
    description: `Update a program's name, description, dates, roles, and strategic themes. A program cannot be moved to a different portfolio through this call. ${READ_FIRST} ${ROLE_LISTS} The id in the body must match the id path parameter. ${LEADERSHIP} ${CONFIRM}`,
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string', format: 'uuid', description: 'Program ID (UUID only, not a key).' },
        requestBody: {
          type: 'object',
          properties: {
            id: { type: 'string', format: 'uuid', description: 'Must match the id path parameter.' },
            name: { type: 'string', maxLength: 128 },
            description: { type: 'string', maxLength: 2048 },
            ...dateProps,
            ...roleProps(false),
            ...strategicThemeIds,
          },
          required: ['id', 'name', 'description'],
        },
      },
      required: ['id', 'requestBody'],
    },
    method: 'put',
    pathTemplate: '/api/ppm/programs/{id}',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: 'application/json',
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Update program', ...requiresConfirmation },
  }],

  // ------------------------------------------------------------------- Projects

  ['Projects_Create', {
    name: 'Projects_Create',
    description: `Create a project in a portfolio, optionally inside a program. It starts in Proposed status. Approving it later requires an assigned lifecycle, and activating it requires a start and end date. Resolve expenditureCategoryId with ExpenditureCategories_GetOptions. ${ROLE_LISTS} ${CONFIRM}`,
    inputSchema: {
      type: 'object',
      properties: {
        requestBody: {
          type: 'object',
          properties: {
            name: { type: 'string', maxLength: 128 },
            description: { type: 'string', maxLength: 4096 },
            key: { type: 'string', description: 'Project key: uppercase letters and digits only, 2-20 characters (e.g. MYPROJ). Must be unique.' },
            expenditureCategoryId: { type: 'number', format: 'int32', description: 'Expenditure category ID. Use ExpenditureCategories_GetOptions to resolve.' },
            portfolioId: { type: 'string', format: 'uuid' },
            programId: { type: ['string', 'null'], format: 'uuid', description: 'Optional parent program. Must belong to the same portfolio.' },
            projectLifecycleId: { type: ['string', 'null'], format: 'uuid', description: 'Optional lifecycle. Required before the project can be approved.' },
            businessCase: { type: ['string', 'null'], maxLength: 4096 },
            expectedBenefits: { type: ['string', 'null'], maxLength: 4096 },
            ...dateProps,
            ...roleProps(true),
            ...strategicThemeIds,
          },
          required: ['name', 'description', 'key', 'expenditureCategoryId', 'portfolioId'],
        },
      },
      required: ['requestBody'],
    },
    method: 'post',
    pathTemplate: '/api/ppm/projects',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Create project', destructiveHint: false, readOnlyHint: false, idempotentHint: false },
  }],

  ['Projects_Update', {
    name: 'Projects_Update',
    description: `Update a project's name, description, business case, expected benefits, expenditure category, dates, roles, and strategic themes. The project's key, program, and lifecycle are NOT changed here — use Projects_ChangeKey, Projects_ChangeProgram, and the lifecycle tools. ${READ_FIRST} ${ROLE_LISTS} The id in the body must match the id path parameter. ${LEADERSHIP} ${CONFIRM}`,
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string', format: 'uuid', description: 'Project ID (UUID only, not a key).' },
        requestBody: {
          type: 'object',
          properties: {
            id: { type: 'string', format: 'uuid', description: 'Must match the id path parameter.' },
            name: { type: 'string', maxLength: 128 },
            description: { type: 'string', maxLength: 4096 },
            expenditureCategoryId: { type: 'number', format: 'int32' },
            businessCase: { type: ['string', 'null'], maxLength: 4096 },
            expectedBenefits: { type: ['string', 'null'], maxLength: 4096 },
            ...dateProps,
            ...roleProps(true),
            ...strategicThemeIds,
          },
          required: ['id', 'name', 'description', 'expenditureCategoryId'],
        },
      },
      required: ['id', 'requestBody'],
    },
    method: 'put',
    pathTemplate: '/api/ppm/projects/{id}',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: 'application/json',
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Update project', ...requiresConfirmation },
  }],

  ['Projects_ChangeProgram', {
    name: 'Projects_ChangeProgram',
    description: `Move a project into a different program, or out of its program entirely by passing a null programId. The target program must belong to the project's portfolio. ${LEADERSHIP} ${CONFIRM}`,
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string', format: 'uuid', description: 'Project ID (UUID only, not a key).' },
        requestBody: {
          type: 'object',
          properties: {
            programId: { type: ['string', 'null'], format: 'uuid', description: 'Target program ID, or null to detach the project from its program.' },
          },
        },
      },
      required: ['id', 'requestBody'],
    },
    method: 'put',
    pathTemplate: '/api/ppm/projects/{id}/program',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: 'application/json',
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Change project program', ...requiresConfirmation },
  }],

  ['Projects_ChangeKey', {
    name: 'Projects_ChangeKey',
    description: `Change a project's key. **The key is the project's human-facing identifier** — it appears in task keys and in links people have saved, so changing it invalidates existing references. Only do this when the user has explicitly asked for a rekey. ${LEADERSHIP} ${CONFIRM}`,
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string', format: 'uuid', description: 'Project ID (UUID only, not a key).' },
        requestBody: {
          type: 'object',
          properties: {
            key: { type: 'string', description: 'New project key: uppercase letters and digits only, 2-20 characters. Must be unique.' },
          },
          required: ['key'],
        },
      },
      required: ['id', 'requestBody'],
    },
    method: 'put',
    pathTemplate: '/api/ppm/projects/{id}/key',
    executionParameters: [{ name: 'id', in: 'path' }],
    requestBodyContentType: 'application/json',
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Change project key', ...requiresConfirmation },
  }],

  // ------------------------------------------------------ Expenditure categories

  ['ExpenditureCategories_GetOptions', {
    name: 'ExpenditureCategories_GetOptions',
    description: `Get a lightweight list of expenditure category options for lookups. Use this to resolve the expenditureCategoryId required when creating or updating a project.`,
    inputSchema: { type: 'object', properties: { includeArchived: { type: ['boolean', 'null'] } } },
    method: 'get',
    pathTemplate: '/api/ppm/expenditure-categories/options',
    executionParameters: [{ name: 'includeArchived', in: 'query' }],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
  }],

];
