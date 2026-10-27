import { IntakeRequestSummaryDto } from '@/src/services/wayd-api'

/** Who a request is assigned to, as the Requests page filters it. */
export type AssigneeFilter = 'anyone' | 'me' | 'unassigned'

/** Whether the page shows only open requests or every request, closed ones included. */
export type RequestScope = 'open' | 'all'

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
}

export const DEFAULT_REQUEST_FILTERS: RequestFilters = {
  scope: 'open',
  deskIds: [],
  statusNames: [],
  assignee: 'anyone',
  channelIds: [],
}

/**
 * The requests the filters keep. The scope is applied by the server, so it is not repeated here.
 * `employeeId` is the signed-in reader's, for "Assigned to me"; without one, that filter keeps nothing.
 */
export const applyRequestFilters = (
  requests: IntakeRequestSummaryDto[],
  filters: RequestFilters,
  employeeId: string | null | undefined,
): IntakeRequestSummaryDto[] =>
  requests.filter(
    (r) =>
      (filters.deskIds.length === 0 || filters.deskIds.includes(r.desk.id)) &&
      (filters.statusNames.length === 0 ||
        filters.statusNames.includes(r.status.name)) &&
      (filters.channelIds.length === 0 ||
        filters.channelIds.includes(r.channel.id)) &&
      (filters.assignee === 'anyone' ||
        (filters.assignee === 'unassigned' && !r.assignee) ||
        (filters.assignee === 'me' &&
          !!employeeId &&
          r.assignee?.id === employeeId)),
  )

/** How many filters differ from the defaults, for the compact "Filters (2)" control. */
export const activeFilterCount = (filters: RequestFilters): number =>
  [
    filters.scope !== DEFAULT_REQUEST_FILTERS.scope,
    filters.deskIds.length > 0,
    filters.statusNames.length > 0,
    filters.assignee !== 'anyone',
    filters.channelIds.length > 0,
  ].filter(Boolean).length
