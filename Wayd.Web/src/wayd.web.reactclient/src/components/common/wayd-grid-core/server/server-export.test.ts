import { fetchAllServerRows } from './server-export'
import type { ServerPageRequest } from './server-page-cache'

/** A server of `total` numbered rows, paged as asked. */
const serverOf = (total: number) =>
  vi.fn(async ({ page, pageSize }: ServerPageRequest) => {
    const start = (page - 1) * pageSize
    const end = Math.min(start + pageSize, total)
    return {
      items: Array.from(
        { length: Math.max(0, end - start) },
        (_, i) => start + i + 1,
      ),
      totalCount: total,
    }
  })

const signal = () => new AbortController().signal

describe('fetchAllServerRows', () => {
  it('reads every page under the query, in order', async () => {
    // Arrange
    const fetchPage = serverOf(7)

    // Act
    const result = await fetchAllServerRows(
      fetchPage,
      { filter: 'stage=Active', sort: 'name asc', search: 'x' },
      { limit: 100, pageSize: 3, signal: signal() },
    )

    // Assert
    expect(result).toEqual({ rows: [1, 2, 3, 4, 5, 6, 7], totalCount: 7 })
    expect(fetchPage.mock.calls.map(([r]) => r.page)).toEqual([1, 2, 3])
    expect(fetchPage.mock.calls[0][0]).toMatchObject({
      filter: 'stage=Active',
      sort: 'name asc',
      search: 'x',
      pageSize: 3,
    })
  })

  it('stops at the limit and keeps the server’s total', async () => {
    // Arrange
    const fetchPage = serverOf(10)

    // Act
    const result = await fetchAllServerRows(
      fetchPage,
      {},
      {
        limit: 4,
        pageSize: 3,
        signal: signal(),
      },
    )

    // Assert
    expect(result).toEqual({ rows: [1, 2, 3, 4], totalCount: 10 })
    expect(fetchPage).toHaveBeenCalledTimes(2)
  })

  it('stops at an empty page when the list shrinks while it is read', async () => {
    // Arrange — the total says 9, but only 4 rows remain
    const fetchPage = vi.fn(async ({ page }: ServerPageRequest) => ({
      items: page === 1 ? [1, 2, 3] : page === 2 ? [4] : [],
      totalCount: 9,
    }))

    // Act
    const result = await fetchAllServerRows(
      fetchPage,
      {},
      {
        limit: 100,
        pageSize: 3,
        signal: signal(),
      },
    )

    // Assert
    expect(result.rows).toEqual([1, 2, 3, 4])
    expect(fetchPage).toHaveBeenCalledTimes(3)
  })

  it('reports the rows read against what it will read', async () => {
    // Arrange
    const onProgress = vi.fn()

    // Act
    await fetchAllServerRows(
      serverOf(10),
      {},
      {
        limit: 5,
        pageSize: 3,
        signal: signal(),
        onProgress,
      },
    )

    // Assert
    expect(onProgress.mock.calls).toEqual([
      [3, 10],
      [5, 10],
    ])
  })

  it('stops reading once aborted', async () => {
    // Arrange
    const controller = new AbortController()
    const fetchPage = vi.fn(async ({ page }: ServerPageRequest) => {
      if (page === 1) controller.abort()
      return { items: [page], totalCount: 5 }
    })

    // Act
    const result = fetchAllServerRows(
      fetchPage,
      {},
      {
        limit: 100,
        pageSize: 1,
        signal: controller.signal,
      },
    )

    // Assert
    await expect(result).rejects.toBeDefined()
    expect(fetchPage).toHaveBeenCalledTimes(1)
  })

  it('stops at once when aborted, even while a page’s fetch ignores the signal', async () => {
    // Arrange
    const controller = new AbortController()
    const fetchPage = vi.fn(() => new Promise<never>(() => {}))
    const result = fetchAllServerRows(
      fetchPage,
      {},
      {
        limit: 100,
        signal: controller.signal,
      },
    )

    // Act
    controller.abort()

    // Assert
    await expect(result).rejects.toBeDefined()
  })
})
