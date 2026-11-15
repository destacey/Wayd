/**
 * The pages of a server-paged list that a grid in server mode holds: the ones
 * under the visible window, fetched as the user scrolls, and nothing else.
 *
 * Kept free of React so its timing rules are testable on their own: a page
 * that arrives after the query changed is dropped, pages may arrive in any
 * order, and pages far from the window are let go so a long scroll does not
 * hold the whole list.
 */

/** What the grid asks the page that hosts it for. */
export interface ServerPageRequest {
  /** The page to read, counting from 1. */
  page: number
  pageSize: number
  /** Gridify `filter`, or undefined for every row. */
  filter?: string
  /** Gridify `sort`, or undefined for the list's default order. */
  sort?: string
  /** The quick-search text, for a list that takes `search`. */
  search?: string
  /** Aborted when the request is no longer wanted (the query changed). */
  signal: AbortSignal
}

/** One page of a `PagedResponse<T>`: only the fields the grid reads. */
export interface ServerPage<T> {
  items: T[]
  totalCount: number
}

export type FetchServerPage<T> = (
  request: ServerPageRequest,
) => Promise<ServerPage<T>>

/** The query a page is read for; a change starts the list over. */
export interface ServerQuery {
  filter?: string
  sort?: string
  search?: string
  /** The host's own identity for the list (a run id, an outside filter). */
  listKey?: unknown
}

export interface ServerPageSnapshot<T> {
  /** The server's total for the current query; undefined until a page arrives. */
  totalCount: number | undefined
  /** Fetched pages by page number. */
  pages: ReadonlyMap<number, T[]>
  /** Whether any page is being fetched. */
  isFetching: boolean
  /** The last fetch's failure for the current query, cleared by the next success. */
  error: unknown
  /** Increases each time the list starts over, so the grid can scroll to the top. */
  generation: number
}

/** Pages kept on each side of the window before the furthest are let go. */
const KEEP_PAGES_AROUND = 5

export class ServerPageCache<T> {
  private fetchPage: FetchServerPage<T>
  private readonly pageSize: number
  private query: ServerQuery = {}
  private queryKey: string | null = null
  private controller = new AbortController()
  private pages = new Map<number, T[]>()
  private inFlight = new Set<number>()
  /** Pages whose last fetch failed: not fetched again until a refresh or until they leave the window. */
  private failed = new Set<number>()
  private totalCount: number | undefined
  private error: unknown
  private generation = 0
  private window: { first: number; last: number } = { first: 1, last: 1 }
  private snapshot: ServerPageSnapshot<T>
  private readonly listeners = new Set<() => void>()

  constructor(fetchPage: FetchServerPage<T>, pageSize: number) {
    this.fetchPage = fetchPage
    this.pageSize = pageSize
    this.snapshot = this.buildSnapshot()
  }

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  getSnapshot = (): ServerPageSnapshot<T> => this.snapshot

  /**
   * Uses `fetchPage` for every fetch from now on, without starting over: a
   * host may hand a new function on every render.
   */
  setFetcher(fetchPage: FetchServerPage<T>): void {
    this.fetchPage = fetchPage
  }

  /**
   * Sets what the list is read for. A different query, compared by value,
   * starts it over: pending fetches are aborted, every page is dropped, and
   * the first page is fetched.
   */
  setQuery(query: ServerQuery): void {
    const key = JSON.stringify([
      query.filter,
      query.sort,
      query.search,
      query.listKey,
    ])
    if (key === this.queryKey) return

    this.queryKey = key
    this.query = query
    this.controller.abort()
    this.controller = new AbortController()
    this.pages = new Map()
    this.inFlight = new Set()
    this.failed = new Set()
    this.totalCount = undefined
    this.error = undefined
    this.generation++
    this.window = { first: 1, last: 1 }
    this.load(1)
    this.publish()
  }

  /**
   * Fetches the pages under rows `firstIndex`..`lastIndex` (0-based, inclusive)
   * and one page either side, and lets go of pages far from them. A page whose
   * fetch failed is not asked for again while it stays in the window, so a
   * caller may call this on every render without retrying in a loop.
   *
   * An arrow property, as is {@link refresh}, so a component can hand it on
   * without a new function each render.
   */
  requestRange = (firstIndex: number, lastIndex: number): void => {
    if (this.queryKey === null) return
    const lastPage = this.lastPage()
    const first = Math.max(1, this.pageOf(firstIndex) - 1)
    const last = Math.min(lastPage, this.pageOf(lastIndex) + 1)
    this.window = { first, last }

    for (const page of this.failed) {
      if (page < first || page > last) this.failed.delete(page)
    }
    for (let page = first; page <= last; page++) {
      if (
        !this.pages.has(page) &&
        !this.inFlight.has(page) &&
        !this.failed.has(page)
      )
        this.load(page)
    }

    let evicted = false
    for (const page of this.pages.keys()) {
      if (page < first - KEEP_PAGES_AROUND || page > last + KEEP_PAGES_AROUND) {
        this.pages.delete(page)
        evicted = true
      }
    }
    if (evicted) this.publish()
  }

  /**
   * Fetches the pages in view again, keeping what they show until each
   * arrives, and lets go of every other page: it was read before the refresh,
   * so scrolling back to it must fetch it afresh rather than show it stale.
   */
  refresh = (): Promise<void> => {
    if (this.queryKey === null) return Promise.resolve()
    const { first, last } = this.window
    this.failed.clear()
    for (const page of this.pages.keys()) {
      if (page < first || page > last) this.pages.delete(page)
    }
    const loads: Promise<void>[] = []
    for (let page = first; page <= Math.max(first, last); page++) {
      if (!this.inFlight.has(page)) loads.push(this.load(page))
    }
    return Promise.all(loads).then(() => undefined)
  }

  private pageOf(index: number): number {
    return Math.floor(Math.max(index, 0) / this.pageSize) + 1
  }

  /** The last page there is, or the first while the total is unknown. */
  private lastPage(): number {
    return this.totalCount === undefined
      ? 1
      : Math.max(1, Math.ceil(this.totalCount / this.pageSize))
  }

  private load(page: number): Promise<void> {
    const generation = this.generation
    const { signal } = this.controller
    this.inFlight.add(page)
    this.publish()

    return this.fetchPage({
      page,
      pageSize: this.pageSize,
      filter: this.query.filter,
      sort: this.query.sort,
      search: this.query.search,
      signal,
    }).then(
      (result) => {
        if (generation !== this.generation) return
        this.inFlight.delete(page)
        this.failed.delete(page)
        this.pages.set(page, result.items)
        this.totalCount = result.totalCount
        this.error = undefined
        // A list that shrank leaves pages past its end.
        const lastPage = this.lastPage()
        for (const held of this.pages.keys()) {
          if (held > lastPage) this.pages.delete(held)
        }
        this.publish()
      },
      (error: unknown) => {
        if (generation !== this.generation) return
        this.inFlight.delete(page)
        this.failed.add(page)
        this.error = error
        this.publish()
      },
    )
  }

  private buildSnapshot(): ServerPageSnapshot<T> {
    return {
      totalCount: this.totalCount,
      pages: new Map(this.pages),
      isFetching: this.inFlight.size > 0,
      error: this.error,
      generation: this.generation,
    }
  }

  private publish(): void {
    this.snapshot = this.buildSnapshot()
    for (const listener of this.listeners) listener()
  }
}
