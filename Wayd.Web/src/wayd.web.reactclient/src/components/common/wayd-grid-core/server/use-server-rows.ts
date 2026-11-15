'use client'

import { useEffect, useState, useSyncExternalStore } from 'react'
import type { RowData } from '@tanstack/react-table'

import type { Row } from '../index'

import {
  ServerPageCache,
  type FetchServerPage,
  type ServerPageSnapshot,
  type ServerQuery,
} from './server-page-cache'

/** Rows per request when the source names none. */
export const DEFAULT_SERVER_PAGE_SIZE = 100

const EMPTY_SNAPSHOT: ServerPageSnapshot<never> = {
  totalCount: undefined,
  pages: new Map(),
  isFetching: false,
  error: undefined,
  generation: 0,
}

const noSubscription = () => () => {}
const emptySnapshot = () => EMPTY_SNAPSHOT

export interface ServerRowsOptions<T> {
  fetchPage: FetchServerPage<T>
  /** A change starts the list over; see WaydGridServerSource. */
  listKey?: unknown
  pageSize: number
  refreshInterval?: number
  /** A change refetches the pages in view; see WaydGridServerSource. */
  refreshKey?: unknown
}

export interface ServerRows<T> {
  snapshot: ServerPageSnapshot<T>
  /** The fetched rows in list order: what the table holds. */
  data: T[]
  /** Fetches the pages under rows `first`..`last` (0-based, inclusive). */
  requestRange: (first: number, last: number) => void
  /** Refetches the pages in view. */
  refresh: () => Promise<void>
}

/**
 * Holds a server-mode grid's pages ({@link ServerPageCache}) for the query the
 * grid's sort, filters and search make. `options` is undefined for a grid in
 * client mode, which then holds nothing; the mode is fixed on mount.
 */
export function useServerRows<T>(
  options: ServerRowsOptions<T> | undefined,
  query: ServerQuery,
): ServerRows<T> | undefined {
  const [cache] = useState(() =>
    options ? new ServerPageCache(options.fetchPage, options.pageSize) : null,
  )

  // Every render's fetcher replaces the last, so a host need not keep one
  // function; it runs before the query effect below, so a list that starts
  // over fetches with the fetcher of the render that changed it.
  const fetchPage = options?.fetchPage
  useEffect(() => {
    if (cache && fetchPage) cache.setFetcher(fetchPage)
  })

  const { filter, sort, search } = query
  // Serialized so a list key built inline (an array, say) compares by value.
  const listKey = JSON.stringify(options?.listKey ?? null)
  // No cleanup: React disconnects and reconnects effects without unmounting
  // (StrictMode in development, a hidden route coming back), and the list it
  // returns to is the one the cache holds. A new query aborts the old one.
  useEffect(() => {
    cache?.setQuery({ filter, sort, search, listKey })
  }, [cache, filter, sort, search, listKey])

  const refreshInterval = options?.refreshInterval
  useEffect(() => {
    if (!cache || !refreshInterval) return
    const id = setInterval(() => {
      if (document.visibilityState === 'visible') void cache.refresh()
    }, refreshInterval)
    return () => clearInterval(id)
  }, [cache, refreshInterval])

  // Compared during render, as React recommends for adjusting to a prop
  // change; the refresh itself runs in the effect it schedules.
  const refreshKey = options?.refreshKey
  const [seenRefreshKey, setSeenRefreshKey] = useState(refreshKey)
  const [refreshRequested, setRefreshRequested] = useState(0)
  if (!Object.is(seenRefreshKey, refreshKey)) {
    setSeenRefreshKey(refreshKey)
    setRefreshRequested((n) => n + 1)
  }
  useEffect(() => {
    if (cache && refreshRequested > 0) void cache.refresh()
  }, [cache, refreshRequested])

  const snapshot = useSyncExternalStore(
    cache?.subscribe ?? noSubscription,
    (cache?.getSnapshot ?? emptySnapshot) as () => ServerPageSnapshot<T>,
    (cache?.getSnapshot ?? emptySnapshot) as () => ServerPageSnapshot<T>,
  )

  if (!cache) return undefined

  const data = [...snapshot.pages.entries()]
    .sort(([a], [b]) => a - b)
    .flatMap(([, items]) => items)

  return {
    snapshot,
    data,
    // The cache's own arrow properties: the same function every render, so an
    // effect that depends on one does not re-run (Vitest runs no compiler).
    requestRange: cache.requestRange,
    refresh: cache.refresh,
  }
}

/** Marks a row that stands in for one not fetched yet. */
const PLACEHOLDER = Symbol('wayd-grid-placeholder-row')

/** A row of a server-mode grid whose page has not arrived. */
export type PlaceholderRow = {
  readonly [PLACEHOLDER]: true
  id: string
  index: number
}

export const isPlaceholderRow = <T extends RowData>(
  row: Row<T> | PlaceholderRow,
): row is PlaceholderRow => PLACEHOLDER in row

/**
 * A stand-in row: an id for navigation and keys, and the few methods the body
 * asks every row for. It has no data and no cells.
 */
const placeholderRow = (index: number): PlaceholderRow =>
  ({
    [PLACEHOLDER]: true,
    id: `__wayd-placeholder-${index}`,
    index,
    depth: 0,
    original: undefined,
    subRows: [],
    getIsGrouped: () => false,
    getIsExpanded: () => false,
    getCanExpand: () => false,
    getParentRow: () => undefined,
    getVisibleCells: () => [],
    getLeafRows: () => [],
  }) as PlaceholderRow

/**
 * The rows a server-mode grid displays: one per row of the server's total, the
 * table's row where its page has arrived and a placeholder elsewhere, so the
 * scrollbar, navigation and `aria-rowindex` all count the whole list.
 *
 * `tableRows` are the table's rows in data order — the fetched pages
 * concatenated in page order, as {@link useServerRows} builds them.
 */
export function toServerDisplayRows<T extends RowData>(
  tableRows: Row<T>[],
  pages: ReadonlyMap<number, unknown[]>,
  pageSize: number,
  totalCount: number,
): Row<T>[] {
  const display: (Row<T> | PlaceholderRow)[] = new Array(totalCount)
  let tableIndex = 0
  for (const page of [...pages.keys()].sort((a, b) => a - b)) {
    const start = (page - 1) * pageSize
    const count = pages.get(page)!.length
    for (let offset = 0; offset < count; offset++, tableIndex++) {
      const index = start + offset
      if (index < totalCount) display[index] = tableRows[tableIndex]
    }
  }
  for (let index = 0; index < totalCount; index++) {
    display[index] ??= placeholderRow(index)
  }
  return display as Row<T>[]
}
