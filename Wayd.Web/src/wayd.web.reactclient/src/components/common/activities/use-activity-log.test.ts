import { act, renderHook } from '@testing-library/react'
import {
  ActivityLogDto,
  PagedResponseOfActivityLogDto,
} from '@/src/services/wayd-api'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  useActivityLog,
  UseActivityLogOptions,
} from './use-activity-log'

const activity = (id: string): ActivityLogDto =>
  ({
    id,
    eventType: 'ProjectDetailsUpdatedEvent',
    domainArea: 'Ppm',
    aggregateType: 'Project',
    payload: '{}',
  }) as unknown as ActivityLogDto

const pageOf = (
  items: ActivityLogDto[],
  totalCount: number,
): PagedResponseOfActivityLogDto =>
  ({ items, totalCount }) as unknown as PagedResponseOfActivityLogDto

const setup = (overrides: Partial<UseActivityLogOptions> = {}) => {
  const fetchPage = jest.fn()
  const options: UseActivityLogOptions = {
    idOrKey: 'record-1',
    query: { data: pageOf([activity('a')], 3), isLoading: false },
    fetchPage,
    exportFilename: 'project-PHX-activity',
    ...overrides,
  }

  return { fetchPage, ...renderHook(() => useActivityLog(options)) }
}

/** A fetchPage stand-in: RTK's trigger returns an object carrying `unwrap`. */
const resolving = (response: PagedResponseOfActivityLogDto) =>
  jest.fn().mockReturnValue({ unwrap: () => Promise.resolve(response) })

describe('useActivityLog', () => {
  it('seeds the timeline from the first page', () => {
    // Arrange & Act
    const { result } = setup()

    // Assert
    expect(result.current.timelineProps.activities).toHaveLength(1)
    expect(result.current.timelineProps.totalCount).toBe(3)
    expect(result.current.timelineProps.hasMore).toBe(true)
  })

  it('reports no more to load once the loaded count reaches the total', () => {
    // Arrange & Act
    const { result } = setup({
      query: { data: pageOf([activity('a')], 1), isLoading: false },
    })

    // Assert
    expect(result.current.timelineProps.hasMore).toBe(false)
  })

  it('appends the next page and asks for it at the shared page size', async () => {
    // Arrange
    const fetchPage = resolving(pageOf([activity('b')], 3))
    const { result } = setup({ fetchPage })

    // Act
    await act(() => result.current.timelineProps.onLoadMore!())

    // Assert
    expect(fetchPage).toHaveBeenCalledWith({
      idOrKey: 'record-1',
      page: 2,
      pageSize: ACTIVITY_LOG_PAGE_SIZE,
    })
    expect(result.current.timelineProps.activities?.map((a) => a.id)).toEqual([
      'a',
      'b',
    ])
  })

  // The server pages by timestamp, so an entry written between two requests can
  // shift a row onto the next page and be returned twice.
  it('drops entries the previous pages already carried', async () => {
    // Arrange
    const fetchPage = resolving(pageOf([activity('a'), activity('b')], 3))
    const { result } = setup({ fetchPage })

    // Act
    await act(() => result.current.timelineProps.onLoadMore!())

    // Assert
    expect(result.current.timelineProps.activities?.map((a) => a.id)).toEqual([
      'a',
      'b',
    ])
  })

  it('does not load more when everything is already loaded', async () => {
    // Arrange
    const fetchPage = jest.fn()
    const { result } = setup({
      query: { data: pageOf([activity('a')], 1), isLoading: false },
      fetchPage,
    })

    // Act
    await act(() => result.current.timelineProps.onLoadMore!())

    // Assert
    expect(fetchPage).not.toHaveBeenCalled()
  })

  it('disables export while there is nothing to export', () => {
    // Arrange & Act
    const { result } = setup({
      query: { data: pageOf([], 0), isLoading: false },
    })

    // Assert
    expect(result.current.isExportDisabled).toBe(true)
  })

  it('opens and closes the export modal', () => {
    // Arrange
    const { result } = setup()
    expect(result.current.timelineProps.isExportOpen).toBe(false)

    // Act
    act(() => result.current.openExport())

    // Assert
    expect(result.current.timelineProps.isExportOpen).toBe(true)

    // Act
    act(() => result.current.timelineProps.onExportClose!())

    // Assert
    expect(result.current.timelineProps.isExportOpen).toBe(false)
  })

  it('returns an empty export batch before the record has loaded', async () => {
    // Arrange
    const fetchPage = jest.fn()
    const { result } = setup({
      idOrKey: undefined,
      query: { data: undefined, isLoading: true },
      fetchPage,
    })

    // Act
    const batch = await result.current.timelineProps.onFetchExportBatch!(1, 100)

    // Assert
    expect(batch).toEqual({ items: [], totalCount: 0 })
    expect(fetchPage).not.toHaveBeenCalled()
  })

  it('fetches an export batch at the size the export asks for', async () => {
    // Arrange
    const fetchPage = resolving(pageOf([activity('a'), activity('b')], 2))
    const { result } = setup({ fetchPage })

    // Act
    const batch = await result.current.timelineProps.onFetchExportBatch!(1, 100)

    // Assert
    expect(fetchPage).toHaveBeenCalledWith({
      idOrKey: 'record-1',
      page: 1,
      pageSize: 100,
    })
    expect(batch.totalCount).toBe(2)
    expect(batch.items).toHaveLength(2)
  })
})
