import type { FetchServerPage } from './server-page-cache'

/** The most rows a server-mode grid exports unless its source says otherwise. */
export const DEFAULT_SERVER_EXPORT_LIMIT = 10_000

/** Rows per request while exporting: the API's largest page, so fewest requests. */
export const SERVER_EXPORT_PAGE_SIZE = 500

/** What a server-mode export reads: the grid's current query. */
export interface ServerExportQuery {
  filter?: string
  sort?: string
  search?: string
}

export interface ServerExportOptions {
  /** The most rows to read; the rest of the list is left out. */
  limit: number
  /** Aborts the reads; the export then rejects with the signal's reason. */
  signal: AbortSignal
  /** Called after each page, with the rows read so far and the server's total. */
  onProgress?: (loaded: number, total: number) => void
  pageSize?: number
}

export interface ServerExportResult<T> {
  rows: T[]
  /** The server's total for the query, which exceeds `rows` when the limit cut it short. */
  totalCount: number
}

/**
 * Settles with `promise`, or rejects as soon as `signal` aborts, so an export
 * stops at once even when a page's fetch ignores the signal.
 */
const untilAborted = <T>(promise: Promise<T>, signal: AbortSignal) =>
  new Promise<T>((resolve, reject) => {
    const onAbort = () => reject(signal.reason)
    signal.addEventListener('abort', onAbort, { once: true })
    promise.then(
      (value) => {
        signal.removeEventListener('abort', onAbort)
        resolve(value)
      },
      (error) => {
        signal.removeEventListener('abort', onAbort)
        reject(error)
      },
    )
  })

/**
 * Reads a server-paged list page by page under the grid's current filter, sort
 * and search, for an export of the whole list rather than the rows in view.
 * Stops at the server's total, at `limit`, or at a page that comes back empty
 * (the list shrank while it was read).
 */
export const fetchAllServerRows = async <T>(
  fetchPage: FetchServerPage<T>,
  query: ServerExportQuery,
  {
    limit,
    signal,
    onProgress,
    pageSize = SERVER_EXPORT_PAGE_SIZE,
  }: ServerExportOptions,
): Promise<ServerExportResult<T>> => {
  const rows: T[] = []
  let totalCount = 0
  for (let page = 1; ; page++) {
    signal.throwIfAborted()
    const result = await untilAborted(
      fetchPage({ ...query, page, pageSize, signal }),
      signal,
    )
    totalCount = result.totalCount
    rows.push(...result.items)
    const wanted = Math.min(totalCount, limit)
    onProgress?.(Math.min(rows.length, wanted), totalCount)
    if (result.items.length === 0 || rows.length >= wanted) break
  }
  return { rows: rows.slice(0, limit), totalCount }
}
