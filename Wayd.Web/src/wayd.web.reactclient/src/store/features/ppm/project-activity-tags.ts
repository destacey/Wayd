import { QueryTags } from '../query-tags'

/**
 * The activity log cache entry a mutation on a project has to refresh.
 *
 * Keyed by the project's id rather than its key, because that is what the page passes to
 * getProjectActivities. Invalidating by key matches no cache entry and fails silently — the log simply
 * keeps serving the entries it had before the change.
 *
 * Kept out of the api modules so the ones that need it do not have to import each other: each calls
 * injectEndpoints at module scope, and importing one for a tag helper would run those endpoint
 * registrations as a side effect of wanting a two-line function.
 */
export const projectActivityTag = (projectId: string) =>
  ({ type: QueryTags.ActivityLog, id: projectId }) as const
