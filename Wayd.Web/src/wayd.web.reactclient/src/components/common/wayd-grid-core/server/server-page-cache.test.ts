import {
  ServerPageCache,
  type ServerPage,
  type ServerPageRequest,
} from './server-page-cache'

interface Pending {
  request: ServerPageRequest
  resolve: (page: ServerPage<number>) => void
  reject: (error: unknown) => void
}

/** A fetcher whose requests the test answers, in whatever order it likes. */
const deferredFetcher = () => {
  const pending: Pending[] = []
  const fetchPage = (request: ServerPageRequest) =>
    new Promise<ServerPage<number>>((resolve, reject) =>
      pending.push({ request, resolve, reject }),
    )
  /** Answers the oldest request for `page` with rows numbered from its offset. */
  const answer = async (page: number, totalCount: number) => {
    const index = pending.findIndex((p) => p.request.page === page)
    if (index < 0) throw new Error(`no request for page ${page}`)
    const [p] = pending.splice(index, 1)
    const start = (page - 1) * p.request.pageSize
    const count = Math.max(0, Math.min(p.request.pageSize, totalCount - start))
    p.resolve({
      items: Array.from({ length: count }, (_, i) => start + i),
      totalCount,
    })
    await Promise.resolve()
  }
  return { fetchPage, pending, answer }
}

const PAGE_SIZE = 10

describe('ServerPageCache', () => {
  it('fetches the first page when the query is set', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)

    cache.setQuery({ filter: 'a=1', sort: 'b desc' })

    expect(fetcher.pending.map((p) => p.request)).toEqual([
      expect.objectContaining({
        page: 1,
        pageSize: PAGE_SIZE,
        filter: 'a=1',
        sort: 'b desc',
      }),
    ])
    expect(cache.getSnapshot().isFetching).toBe(true)

    await fetcher.answer(1, 95)

    const snapshot = cache.getSnapshot()
    expect(snapshot.totalCount).toBe(95)
    expect(snapshot.pages.get(1)).toHaveLength(PAGE_SIZE)
    expect(snapshot.isFetching).toBe(false)
  })

  it('fetches the pages under the window and one either side, once each', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 200)

    cache.requestRange(45, 62)
    cache.requestRange(45, 62)

    expect(fetcher.pending.map((p) => p.request.page)).toEqual([4, 5, 6, 7, 8])
  })

  it('keeps pages that arrive out of order', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 200)
    cache.requestRange(20, 29)

    await fetcher.answer(4, 200)
    await fetcher.answer(2, 200)
    await fetcher.answer(3, 200)

    const pages = cache.getSnapshot().pages
    expect([...pages.keys()].sort()).toEqual([1, 2, 3, 4])
    expect(pages.get(3)?.[0]).toBe(20)
  })

  it('drops a page that arrives after the query changed', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({ sort: 'a asc' })
    const stale = fetcher.pending[0]

    cache.setQuery({ sort: 'a desc' })
    stale.resolve({ items: [999], totalCount: 1 })
    await Promise.resolve()

    const snapshot = cache.getSnapshot()
    expect(stale.request.signal.aborted).toBe(true)
    expect(snapshot.totalCount).toBeUndefined()
    expect(snapshot.pages.size).toBe(0)
    expect(snapshot.generation).toBe(2)
    expect(fetcher.pending.at(-1)?.request.sort).toBe('a desc')
  })

  it('does not start over for the same query', () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)

    cache.setQuery({ filter: 'a=1' })
    cache.setQuery({ filter: 'a=1' })

    expect(cache.getSnapshot().generation).toBe(1)
    expect(fetcher.pending).toHaveLength(1)
  })

  it('starts over when the list key changes', () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({ listKey: ['run-1', 'Failed'] })

    cache.setQuery({ listKey: ['run-1', 'Failed'] })
    expect(cache.getSnapshot().generation).toBe(1)
    cache.setQuery({ listKey: ['run-1', 'All'] })

    expect(cache.getSnapshot().generation).toBe(2)
  })

  it('fetches with a new fetcher without starting over', async () => {
    const fetcher = deferredFetcher()
    const other = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 100)

    cache.setFetcher(other.fetchPage)
    cache.requestRange(30, 39)

    expect(cache.getSnapshot().generation).toBe(1)
    expect(cache.getSnapshot().pages.has(1)).toBe(true)
    expect(other.pending.map((p) => p.request.page)).toEqual([3, 4, 5])
  })

  it('reaches the last page directly, without the pages before it', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 1000)

    cache.requestRange(985, 999)

    expect(fetcher.pending.map((p) => p.request.page)).toEqual([98, 99, 100])
  })

  it('lets go of pages far from the window', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 1000)
    cache.requestRange(0, 9)
    await fetcher.answer(2, 1000)

    cache.requestRange(500, 509)

    expect(cache.getSnapshot().pages.has(1)).toBe(false)
    expect(cache.getSnapshot().pages.has(2)).toBe(false)
  })

  it('refetches the pages in view on refresh, keeping them until they arrive', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 30)
    cache.requestRange(0, 9)
    await fetcher.answer(2, 30)

    const refreshed = cache.refresh()

    expect(fetcher.pending.map((p) => p.request.page)).toEqual([1, 2])
    expect(cache.getSnapshot().pages.has(1)).toBe(true)
    await fetcher.answer(1, 25)
    await fetcher.answer(2, 25)
    await refreshed
    expect(cache.getSnapshot().totalCount).toBe(25)
  })

  it('drops pages past the end of a list that shrank', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 30)
    cache.requestRange(20, 29)
    await fetcher.answer(2, 30)
    await fetcher.answer(3, 30)

    void cache.refresh()
    await fetcher.answer(2, 12)

    expect([...cache.getSnapshot().pages.keys()].sort()).toEqual([2])
  })

  it('lets go of pages outside the window on refresh, so scrolling back fetches them afresh', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 200)
    cache.requestRange(0, 9)
    await fetcher.answer(2, 200)
    cache.requestRange(40, 49)
    for (const page of [4, 5, 6]) await fetcher.answer(page, 200)

    void cache.refresh()
    for (const page of [4, 5, 6]) await fetcher.answer(page, 200)
    cache.requestRange(0, 9)

    expect(fetcher.pending.map((p) => p.request.page)).toEqual([1, 2])
  })

  it('does not ask again for a failed page while it stays in the window', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 200)
    cache.requestRange(40, 49)
    const failed = fetcher.pending.find((p) => p.request.page === 5)!
    fetcher.pending.splice(fetcher.pending.indexOf(failed), 1)
    failed.reject(new Error('boom'))
    await Promise.resolve()

    cache.requestRange(40, 49)
    cache.requestRange(41, 48)

    expect(fetcher.pending.map((p) => p.request.page)).toEqual([4, 6])
  })

  it('asks again for a failed page once it has left the window and come back', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    await fetcher.answer(1, 1000)
    cache.requestRange(400, 409)
    for (const page of [40, 42]) await fetcher.answer(page, 1000)
    fetcher.pending.shift()!.reject(new Error('boom'))
    await Promise.resolve()

    cache.requestRange(0, 9)
    cache.requestRange(400, 409)

    expect(fetcher.pending.map((p) => p.request.page)).toContain(41)
  })

  it('hands out the same range and refresh functions every time', () => {
    const cache = new ServerPageCache(deferredFetcher().fetchPage, PAGE_SIZE)
    const { requestRange, refresh } = cache

    expect(cache.requestRange).toBe(requestRange)
    expect(cache.refresh).toBe(refresh)
  })

  it('reports a failed fetch and lets the page be requested again', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({})
    const failure = new Error('boom')

    fetcher.pending.shift()!.reject(failure)
    await Promise.resolve()

    expect(cache.getSnapshot().error).toBe(failure)
    expect(cache.getSnapshot().isFetching).toBe(false)
    void cache.refresh()
    expect(fetcher.pending.map((p) => p.request.page)).toEqual([1])
  })

  it('keeps a fetch in flight when the same query is set again, as an effect reconnecting does', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    cache.setQuery({ filter: 'a=1' })
    const first = fetcher.pending[0]

    cache.setQuery({ filter: 'a=1' })

    expect(first.request.signal.aborted).toBe(false)
    expect(fetcher.pending).toHaveLength(1)
    first.resolve({ items: [0, 1, 2], totalCount: 3 })
    await Promise.resolve()
    expect(cache.getSnapshot().totalCount).toBe(3)
  })

  it('notifies subscribers when what it holds changes', async () => {
    const fetcher = deferredFetcher()
    const cache = new ServerPageCache(fetcher.fetchPage, PAGE_SIZE)
    const listener = vi.fn()
    cache.subscribe(listener)

    cache.setQuery({})
    await fetcher.answer(1, 5)

    expect(listener).toHaveBeenCalled()
    expect(cache.getSnapshot().pages.get(1)).toEqual([0, 1, 2, 3, 4])
  })
})
