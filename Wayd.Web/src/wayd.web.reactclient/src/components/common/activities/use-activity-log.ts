'use client'

import { useEffect, useState } from 'react'
import {
  ActivityLogDto,
  PagedResponseOfActivityLogDto,
} from '@/src/services/wayd-api'
import { ActivityLogTimelineProps } from './activity-log-timeline'

/** Page size for the first page and each "load more"; also what a record page should request. */
export const ACTIVITY_LOG_PAGE_SIZE = 50

export interface ActivityLogQueryArg {
  idOrKey: string | number
  page?: number
  pageSize?: number
}

/**
 * The subset of an RTK query result this hook reads.
 *
 * `currentData`, not `data`: `data` holds the last result for *any* argument, so it still carries the
 * previous record's page while the next record's is in flight.
 */
export interface ActivityLogQueryResult {
  currentData?: PagedResponseOfActivityLogDto
  isLoading: boolean
}

/** The trigger returned by a `useLazyGetXActivitiesQuery` hook. */
export type ActivityLogPageFetcher = (arg: ActivityLogQueryArg) => {
  unwrap: () => Promise<PagedResponseOfActivityLogDto>
}

export interface UseActivityLogOptions {
  /** The record's id or key. Undefined until the record itself has loaded. */
  idOrKey: string | number | undefined
  /** The first-page query, which the caller skips until the activity section is open. */
  query: ActivityLogQueryResult
  fetchPage: ActivityLogPageFetcher
  /** Base filename for the export, e.g. `project-PHX-activity`. */
  exportFilename: string
}

export interface ActivityLog {
  timelineProps: ActivityLogTimelineProps
  openExport: () => void
  isExportDisabled: boolean
}

/** The loaded rows, and the record they belong to. */
interface LoadedActivities {
  idOrKey: string | number | undefined
  items: ActivityLogDto[]
}

/**
 * Drives a record's activity section: incremental loading on top of the first page, and the
 * export modal's state.
 */
export const useActivityLog = ({
  idOrKey,
  query,
  fetchPage,
  exportFilename,
}: UseActivityLogOptions): ActivityLog => {
  const [page, setPage] = useState<number>(1)
  const [loaded, setLoaded] = useState<LoadedActivities>({
    idOrKey: undefined,
    items: [],
  })
  const [isLoadingMore, setIsLoadingMore] = useState<boolean>(false)
  const [isExportOpen, setIsExportOpen] = useState<boolean>(false)

  useEffect(() => {
    if (query.currentData?.items) {
      setLoaded({ idOrKey, items: query.currentData.items })
      setPage(1)
    }
  }, [query.currentData, idOrKey])

  // Accumulated rows are held against the record they were loaded for. These pages navigate between
  // records of the same kind without remounting — the sprint switcher, a link to a sibling product —
  // so an unkeyed list would show one record's history under another record's name until the next
  // page resolved.
  const activities = loaded.idOrKey === idOrKey ? loaded.items : []

  const totalCount = query.currentData?.totalCount
  const hasMore = totalCount !== undefined && activities.length < totalCount

  const onLoadMore = async () => {
    if (!idOrKey || isLoadingMore || !hasMore) return
    const nextPage = page + 1
    setIsLoadingMore(true)
    try {
      const result = await fetchPage({
        idOrKey,
        page: nextPage,
        pageSize: ACTIVITY_LOG_PAGE_SIZE,
      }).unwrap()

      if (result.items && result.items.length > 0) {
        setLoaded((prev) => {
          // The record can change while this page is in flight; those rows belong to the record
          // that was open when it was asked for, not the one open when it arrives.
          if (prev.idOrKey !== idOrKey) return prev

          const existingIds = new Set(prev.items.map((a) => a.id))
          const fresh = result.items.filter((a) => !existingIds.has(a.id))
          return { idOrKey, items: [...prev.items, ...fresh] }
        })
        setPage(nextPage)
      }
    } catch (error) {
      console.error('Failed to load more activities:', error)
    } finally {
      setIsLoadingMore(false)
    }
  }

  const onFetchExportBatch = async (
    exportPage: number,
    exportPageSize: number,
  ) => {
    if (!idOrKey) return { items: [], totalCount: 0 }
    const result = await fetchPage({
      idOrKey,
      page: exportPage,
      pageSize: exportPageSize,
    }).unwrap()

    return {
      items: result.items ?? [],
      totalCount: result.totalCount ?? 0,
    }
  }

  return {
    timelineProps: {
      activities,
      isLoading: query.isLoading,
      isLoadingMore,
      totalCount,
      hasMore,
      onLoadMore,
      onFetchExportBatch,
      exportFilename,
      isExportOpen,
      onExportClose: () => setIsExportOpen(false),
    },
    openExport: () => setIsExportOpen(true),
    isExportDisabled: query.isLoading || (!activities.length && !totalCount),
  }
}

export default useActivityLog
