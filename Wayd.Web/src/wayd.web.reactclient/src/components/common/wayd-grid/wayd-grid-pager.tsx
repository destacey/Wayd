'use client'

import { Pagination } from 'antd'
import { useState } from 'react'

/** The page sizes a paged grid offers. The API's default is the first and its maximum the last. */
export const GRID_PAGE_SIZE_OPTIONS = [50, 100, 250, 500]

export interface GridPaging {
  /** The page to request, counting from 1. */
  page: number
  /** How many items a page holds, one of {@link GRID_PAGE_SIZE_OPTIONS}. */
  pageSize: number
  /** Moves to a page, or to the first page at a new size. */
  setPaging: (page: number, pageSize: number) => void
  /** Goes back to the first page, for when what the list holds changes (a new filter). */
  resetPage: () => void
  /**
   * Called during render with the response's `totalPages`: a list that shrinks while someone reads its last
   * page (jobs finishing, messages replayed) moves back to the page that is now last instead of showing an
   * empty one. Call it in the component that holds the paging: setting another component's state during
   * render is an error.
   */
  keepInRange: (totalPages: number | undefined) => void
}

/** Holds which page of a server-paged list a grid shows. */
export const useGridPaging = (
  initialPageSize = GRID_PAGE_SIZE_OPTIONS[0],
): GridPaging => {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(initialPageSize)

  return {
    page,
    pageSize,
    setPaging: (nextPage, nextPageSize) => {
      setPage(nextPageSize === pageSize ? nextPage : 1)
      setPageSize(nextPageSize)
    },
    resetPage: () => setPage(1),
    // Adjusted during render rather than in an effect, so the out-of-range page never paints.
    keepInRange: (totalPages) => {
      if (totalPages === undefined) return
      const lastPage = Math.max(totalPages, 1)
      if (page > lastPage) setPage(lastPage)
    },
  }
}

export interface WaydGridPagerProps {
  paging: GridPaging
  /** The response's `totalCount`; nothing is shown until there is one. */
  totalCount: number | undefined
  /** What the list holds, plural, for the range label ("1–50 of 312 jobs"). */
  noun: string
}

/**
 * Page controls for a grid whose list pages on the server, for its toolbar's `rightSlot`.
 *
 * The grid's own sorting and filters act only on the page it holds, so a list paged this way offers its
 * filters as request parameters wherever the API takes them.
 */
const WaydGridPager = ({ paging, totalCount, noun }: WaydGridPagerProps) => {
  if (totalCount === undefined) return null

  return (
    <Pagination
      size="small"
      current={paging.page}
      pageSize={paging.pageSize}
      total={totalCount}
      onChange={paging.setPaging}
      showSizeChanger
      pageSizeOptions={GRID_PAGE_SIZE_OPTIONS}
      showTotal={(total, [from, to]) =>
        total === 0 ? `0 ${noun}` : `${from}–${to} of ${total} ${noun}`
      }
    />
  )
}

export default WaydGridPager
