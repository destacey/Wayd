import { QueryTags } from '../query-tags'

/**
 * The activity log cache entry a mutation on a PPM record has to refresh.
 *
 * Keyed by the record's id rather than its key, because that is what each page passes to its
 * get*Activities query. Invalidating by key matches no cache entry and fails silently — the log simply
 * keeps serving the entries it had before the change.
 *
 * Shared by projects, programs and portfolios: one tag type keyed by id covers all three, and the log a
 * mutation has to refresh is always the record it changed. A portfolio-level change to something it owns
 * — a strategic initiative created or deleted — refreshes the portfolio's log, because that is the
 * aggregate the event names.
 *
 * Kept out of the api modules so the ones that need it do not have to import each other: each calls
 * injectEndpoints at module scope, and importing one for a tag helper would run those endpoint
 * registrations as a side effect of wanting a two-line function.
 */
export const ppmActivityTag = (recordId: string) =>
  ({ type: QueryTags.ActivityLog, id: recordId }) as const
