import {
  IntakeRequestSummaryDto,
  NeedByBasisFamily,
} from '@/src/services/wayd-api'

/** Who a request is assigned to, as the Requests page filters it. */
export type AssigneeFilter = 'anyone' | 'me' | 'unassigned'

/** Whether the page shows only open requests or every request, closed ones included. */
export type RequestScope = 'open' | 'all'

/** Which need-by dates to keep: any, a date fixed for us, a date we chose, or no date at all. */
export type NeedByFilter = 'any' | 'fixed' | 'chosen' | 'none'

/** How the requests are laid out. */
export type RequestView = 'list' | 'board'

/**
 * The one filter set the Requests page applies to both the list and the board, so switching view never
 * changes what is in scope.
 */
export interface RequestFilters {
  scope: RequestScope
  /** Empty for every desk. */
  deskIds: string[]
  /**
   * Status names, not ids: two desks on different workflows each have their own "In Review", and someone
   * filtering on it means both.
   */
  statusNames: string[]
  assignee: AssigneeFilter
  /** Empty for every channel. */
  channelIds: string[]
  needBy: NeedByFilter
}

export const DEFAULT_REQUEST_FILTERS: RequestFilters = {
  scope: 'open',
  deskIds: [],
  statusNames: [],
  assignee: 'anyone',
  channelIds: [],
  needBy: 'any',
}

const keepsNeedBy = (
  filter: NeedByFilter,
  request: IntakeRequestSummaryDto,
): boolean => {
  switch (filter) {
    case 'fixed':
      return request.needBy?.family === NeedByBasisFamily.FixedForUs
    case 'chosen':
      return request.needBy?.family === NeedByBasisFamily.ChosenByUs
    case 'none':
      return !request.needBy
    default:
      return true
  }
}

/**
 * Stands in a desk filter for "the desks the reader works", so a view or a shared link means each reader's
 * own desks rather than the sender's.
 */
export const MY_DESKS = 'mine'

/** The desk ids a desk filter names, with {@link MY_DESKS} expanded to the reader's desks. */
export const resolveDeskIds = (
  deskIds: string[],
  myDeskIds: string[],
): string[] => [
  ...new Set(deskIds.flatMap((id) => (id === MY_DESKS ? myDeskIds : [id]))),
]

/**
 * The requests the filters keep. The scope is applied by the server, so it is not repeated here.
 * `employeeId` is the signed-in reader's, for "Assigned to me"; without one, that filter keeps nothing.
 * `myDeskIds` are the desks the reader works, for "My desks"; without any, that filter keeps nothing.
 */
export const applyRequestFilters = (
  requests: IntakeRequestSummaryDto[],
  filters: RequestFilters,
  employeeId: string | null | undefined,
  myDeskIds: string[] = [],
): IntakeRequestSummaryDto[] => {
  const deskIds = resolveDeskIds(filters.deskIds, myDeskIds)
  return requests.filter(
    (r) =>
      (filters.deskIds.length === 0 || deskIds.includes(r.desk.id)) &&
      (filters.statusNames.length === 0 ||
        filters.statusNames.includes(r.status.name)) &&
      (filters.channelIds.length === 0 ||
        filters.channelIds.includes(r.channel.id)) &&
      (filters.assignee === 'anyone' ||
        (filters.assignee === 'unassigned' && !r.assignee) ||
        (filters.assignee === 'me' &&
          !!employeeId &&
          r.assignee?.id === employeeId)) &&
      keepsNeedBy(filters.needBy, r),
  )
}

/** How many filters differ from the defaults, for the compact "Filters (2)" control. */
export const activeFilterCount = (filters: RequestFilters): number =>
  [
    filters.scope !== DEFAULT_REQUEST_FILTERS.scope,
    filters.deskIds.length > 0,
    filters.statusNames.length > 0,
    filters.assignee !== 'anyone',
    filters.channelIds.length > 0,
    filters.needBy !== 'any',
  ].filter(Boolean).length

/** A view the Requests page offers by name: a set of filters reviewers reach for often. */
export interface BuiltInView {
  id: string
  name: string
  /** What the view is for, shown beside its name. */
  description: string
  filters: RequestFilters
}

/**
 * The views every reader gets. Each is a filter set, nothing more, so picking one and then changing a
 * filter simply leaves the view. Personal saved views are a separate feature.
 */
export const BUILT_IN_VIEWS: BuiltInView[] = [
  {
    id: 'my-desks',
    name: 'My desks',
    description: 'Open requests on the desks you work.',
    filters: { ...DEFAULT_REQUEST_FILTERS, deskIds: [MY_DESKS] },
  },
  {
    id: 'open',
    name: 'All open requests',
    description: 'Everything still under review or accepted.',
    filters: DEFAULT_REQUEST_FILTERS,
  },
  {
    id: 'needs-triage',
    name: 'Needs triage',
    description: 'Open requests nobody has picked up yet.',
    filters: { ...DEFAULT_REQUEST_FILTERS, assignee: 'unassigned' },
  },
  {
    id: 'assigned-to-me',
    name: 'Assigned to me',
    description: 'Open requests you own the next move on.',
    filters: { ...DEFAULT_REQUEST_FILTERS, assignee: 'me' },
  },
  {
    id: 'fixed-dates',
    name: 'Fixed dates, open',
    description: 'Open requests whose date something outside us sets.',
    filters: { ...DEFAULT_REQUEST_FILTERS, needBy: 'fixed' },
  },
]

const sameSet = (a: string[], b: string[]) =>
  a.length === b.length && a.every((x) => b.includes(x))

const sameFilters = (a: RequestFilters, b: RequestFilters) =>
  a.scope === b.scope &&
  a.assignee === b.assignee &&
  a.needBy === b.needBy &&
  sameSet(a.deskIds, b.deskIds) &&
  sameSet(a.statusNames, b.statusNames) &&
  sameSet(a.channelIds, b.channelIds)

/** The built-in view the filters are exactly, or undefined once the reader has changed something. */
export const matchingView = (
  filters: RequestFilters,
): BuiltInView | undefined =>
  BUILT_IN_VIEWS.find((v) => sameFilters(v.filters, filters))

const pick = <T extends string>(
  value: string | null,
  allowed: readonly T[],
  fallback: T,
): T => (allowed.includes(value as T) ? (value as T) : fallback)

/** Anything with the `get` and `getAll` of `URLSearchParams`, which Next's read-only params also have. */
interface ReadableParams {
  get(name: string): string | null
  getAll(name: string): string[]
}

/**
 * The filters and layout an address carries, so a view can be bookmarked or sent to someone. Unknown or
 * missing values fall back to the defaults rather than failing.
 */
export const fromSearchParams = (
  params: ReadableParams,
): { filters: RequestFilters; view: RequestView } => ({
  view: pick(params.get('view'), ['list', 'board'] as const, 'list'),
  filters: {
    scope: pick(params.get('scope'), ['open', 'all'] as const, 'open'),
    deskIds: params.getAll('desk'),
    statusNames: params.getAll('status'),
    assignee: pick(
      params.get('assignee'),
      ['anyone', 'me', 'unassigned'] as const,
      'anyone',
    ),
    channelIds: params.getAll('channel'),
    needBy: pick(
      params.get('needBy'),
      ['any', 'fixed', 'chosen', 'none'] as const,
      'any',
    ),
  },
})

/** The address query for filters and a layout, leaving out whatever is at its default. */
export const toSearchParams = (
  filters: RequestFilters,
  view: RequestView,
): URLSearchParams => {
  const params = new URLSearchParams()
  if (view !== 'list') params.set('view', view)
  if (filters.scope !== 'open') params.set('scope', filters.scope)
  filters.deskIds.forEach((id) => params.append('desk', id))
  filters.statusNames.forEach((name) => params.append('status', name))
  if (filters.assignee !== 'anyone') params.set('assignee', filters.assignee)
  filters.channelIds.forEach((id) => params.append('channel', id))
  if (filters.needBy !== 'any') params.set('needBy', filters.needBy)
  return params
}
