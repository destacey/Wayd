import type { McpToolDefinition } from '../types.js';

/**
 * Activity history: the events recorded against one record, newest first.
 *
 * Every area's endpoint returns the same paged entry shape, so that guidance is
 * written once here rather than drifting apart across a dozen descriptions.
 */

const readsOnly = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
} as const;

const ENTRY_SHAPE =
  'Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), ' +
  'an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, ' +
  'and a `payload`: the event\'s fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. ' +
  'A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. ' +
  'An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). ' +
  'Paged: the response carries `totalCount` and `hasNextPage`.';

const PAGING = {
  page: { type: 'integer', description: 'Page number, starting at 1. Defaults to 1.' },
  pageSize: { type: 'integer', description: 'Entries per page, 1 to 100. Defaults to 50; a larger value is capped at 100.' },
} as const;

const PAGING_PARAMETERS = [{ name: 'page', in: 'query' }, { name: 'pageSize', in: 'query' }];

function activityTool(
  name: string,
  title: string,
  what: string,
  pathTemplate: string,
  idDescription: string
): [string, McpToolDefinition] {
  return [name, {
    name,
    description: `${what} ${ENTRY_SHAPE}`,
    inputSchema: {
      type: 'object',
      properties: { idOrKey: { type: 'string', description: idDescription }, ...PAGING },
      required: ['idOrKey'],
    },
    method: 'get',
    pathTemplate,
    executionParameters: [{ name: 'idOrKey', in: 'path' }, ...PAGING_PARAMETERS],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title, ...readsOnly },
  }];
}

export const definitions: [string, McpToolDefinition][] = [

  activityTool(
    'Portfolios_GetActivities',
    'Get portfolio activity history',
    'Get a portfolio\'s activity history, newest first: every change recorded on the portfolio itself — details, roles, scoring model, status. Its programs and projects keep their own histories.',
    '/api/ppm/portfolios/{idOrKey}/activities',
    'Portfolio ID (UUID) or key.'
  ),

  activityTool(
    'Programs_GetActivities',
    'Get program activity history',
    'Get a program\'s activity history, newest first: every change recorded on the program itself — details, roles, timeline, strategic themes, status. Its projects keep their own histories.',
    '/api/ppm/programs/{idOrKey}/activities',
    'Program ID (UUID) or key.'
  ),

  activityTool(
    'Projects_GetActivities',
    'Get project activity history',
    'Get a project\'s activity history, newest first: every change recorded on the project itself — details, key, program, lifecycle, timeline, roles, strategic themes, status, health checks and scores. Tasks and stages are not included. Prefer this over `Projects_GetStatusHistory` when asking what changed beyond status.',
    '/api/ppm/projects/{idOrKey}/activities',
    'Project ID (UUID) or key.'
  ),

  activityTool(
    'StrategicInitiatives_GetActivities',
    'Get strategic initiative activity history',
    'Get a strategic initiative\'s activity history, newest first: every change recorded on the initiative — details, roles, timeline, status, linked projects, and its KPIs with their targets, checkpoint plans and measurements.',
    '/api/ppm/strategic-initiatives/{idOrKey}/activities',
    'Strategic initiative ID (UUID) or key.'
  ),

  activityTool(
    'PlanningIntervals_GetActivities',
    'Get planning interval activity history',
    'Get a planning interval\'s activity history, newest first: every change recorded on the interval itself — details, dates, teams, iterations, sprint mappings, and objectives being locked or unlocked. Each objective keeps its own history (`PlanningIntervals_GetObjectiveActivities`).',
    '/api/planning/planning-intervals/{idOrKey}/activities',
    'Planning interval ID (UUID) or key.'
  ),

  ['PlanningIntervals_GetObjectiveActivities', {
    name: 'PlanningIntervals_GetObjectiveActivities',
    description: `Get a planning interval objective's activity history, newest first: every change recorded on the objective — details, status, progress, order, stretch, timeline and health checks. The objective must belong to the planning interval named alongside it, or the call returns 404. ${ENTRY_SHAPE}`,
    inputSchema: {
      type: 'object',
      properties: {
        idOrKey: { type: 'string', description: 'Planning interval ID (UUID) or key.' },
        objectiveIdOrKey: { type: 'string', description: 'Objective ID (UUID) or key, within that planning interval.' },
        ...PAGING,
      },
      required: ['idOrKey', 'objectiveIdOrKey'],
    },
    method: 'get',
    pathTemplate: '/api/planning/planning-intervals/{idOrKey}/objectives/{objectiveIdOrKey}/activities',
    executionParameters: [{ name: 'idOrKey', in: 'path' }, { name: 'objectiveIdOrKey', in: 'path' }, ...PAGING_PARAMETERS],
    requestBodyContentType: undefined,
    securityRequirements: [{ ApiKey: [] }],
    annotations: { title: 'Get planning interval objective activity history', ...readsOnly },
  }],

  activityTool(
    'Teams_GetActivities',
    'Get team activity history',
    'Get a team\'s activity history, newest first: the team\'s creation, detail changes, activation and deactivation.',
    '/api/organization/teams/{idOrKey}/activities',
    'Team ID (UUID) or its integer key — the `id` that `Teams_GetTeam` takes.'
  ),

  activityTool(
    'Products_GetActivities',
    'Get product activity history',
    'Get a product\'s activity history, newest first: every change recorded on the product — details, type, parent, status, tags and external link — plus a child product moving in or out, listed as a related entry raised on that child.',
    '/api/product-management/products/{idOrKey}/activities',
    'Product ID (UUID) or its short key.'
  ),

  activityTool(
    'Releases_GetActivities',
    'Get release activity history',
    'Get a release\'s activity history, newest first: every change recorded on the release — details, contents, dates and status.',
    '/api/product-management/releases/{idOrKey}/activities',
    'Release ID (UUID) or key.'
  ),

  activityTool(
    'Versions_GetActivities',
    'Get version activity history',
    'Get a version\'s activity history, newest first: every change recorded on the version — details, dates and status.',
    '/api/product-management/versions/{idOrKey}/activities',
    'Version ID (UUID) or key.'
  ),

  activityTool(
    'ReleasePackages_GetActivities',
    'Get release package activity history',
    'Get a release package\'s activity history, newest first: its assembly, manifest amendments and status.',
    '/api/product-management/release-packages/{idOrKey}/activities',
    'Release package ID (UUID) or key.'
  ),

  activityTool(
    'Deployments_GetActivities',
    'Get deployment activity history',
    'Get a deployment\'s activity history, newest first: when it started and its outcome — succeeded, failed or rolled back.',
    '/api/product-management/deployments/{idOrKey}/activities',
    'Deployment ID (UUID) or key.'
  ),

];
