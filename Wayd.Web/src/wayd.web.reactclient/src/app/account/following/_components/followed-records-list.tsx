'use client'
'use no memo'

import {
  SortAscendingOutlined,
  SortDescendingOutlined,
} from '@ant-design/icons'
import { Alert, Button, Input, Select, Space, Spin, Tooltip } from 'antd'
import { useVirtualizer } from '@tanstack/react-virtual'
import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { LifecycleStatusTag } from '@/src/components/common'
import {
  FollowedRecordDto,
  FollowSort,
  SortDirection,
} from '@/src/services/wayd-api'
import { findScrollParent } from '@/src/utils/scroll-parent'
import styles from '../following.module.css'
import FollowedRecordHealth from './followed-record-health'
import { recordId } from './following-filters'
import { formatTimeAgo } from './time-ago'

export interface FollowedRecordsListProps {
  records: FollowedRecordDto[]
  totalCount: number
  isLoading: boolean
  isError: boolean
  hasMore: boolean
  isLoadingMore: boolean
  onLoadMore: () => void
  search: string
  onSearchChange: (search: string) => void
  sort: FollowSort
  onSortChange: (sort: FollowSort) => void
  direction: SortDirection
  onDirectionChange: (direction: SortDirection) => void
  /** What to call a record's kind, such as "Project". */
  typeName: (ownerType: string) => string
  selectedId: string | undefined
  onSelect: (record: FollowedRecordDto) => void
}

const RowHeight = 64

/** When fewer than this many loaded rows remain below the view, the next page loads. */
const LoadAhead = 10

/**
 * The viewport assumed before the scroll container is measured, so the first
 * rows render on the first paint (and in tests, where nothing has a size).
 */
const InitialRect = { width: 0, height: 800 }

interface RowProps {
  record: FollowedRecordDto
  typeName: string
  selected: boolean
  onSelect: (record: FollowedRecordDto) => void
}

const FollowedRecordRow = ({
  record,
  typeName,
  selected,
  onSelect,
}: RowProps) => (
  <button
    type="button"
    aria-current={selected || undefined}
    className={`${styles.row} ${selected ? styles.rowSelected : ''}`}
    onClick={() => onSelect(record)}
  >
    <span className={styles.rowBody}>
      <span className={styles.rowName}>{record.name}</span>
      <span className={styles.rowMeta}>
        <span className={styles.rowType}>{typeName}</span>
        {' · '}
        {record.key}
        {record.parentName ? ` · ${record.parentName}` : ''}
        {' · '}
        {record.lastChangedOn
          ? `changed ${formatTimeAgo(record.lastChangedOn)}`
          : 'no changes yet'}
      </span>
    </span>
    <span className={styles.rowTrail}>
      <FollowedRecordHealth record={record} variant="flag" />
      <LifecycleStatusTag
        status={{
          id: 0,
          name: record.statusName,
          lifecycleCategory: record.lifecycle,
        }}
      />
    </span>
  </button>
)

/**
 * The followed records, one row each. Selecting a row is the only thing a row
 * does; what can be done with a record lives in its detail.
 *
 * Virtualised against the element the page scrolls in — the app shell's
 * content area, not the window — so the list scrolls with the page on every
 * screen size, and the next page loads as the end comes into view.
 */
const FollowedRecordsList = ({
  records,
  totalCount,
  isLoading,
  isError,
  hasMore,
  isLoadingMore,
  onLoadMore,
  search,
  onSearchChange,
  sort,
  onSortChange,
  direction,
  onDirectionChange,
  typeName,
  selectedId,
  onSelect,
}: FollowedRecordsListProps) => {
  const rootRef = useRef<HTMLDivElement>(null)
  const listRef = useRef<HTMLDivElement>(null)
  const [scrollElement, setScrollElement] = useState<HTMLElement | null>(null)
  // Where the list starts within the scroll container's content, which the
  // virtualizer needs to know which rows are in view. Re-measured whenever the
  // content resizes, since what sits above the list changes height with the
  // width, the filter row and the counts.
  const [scrollMargin, setScrollMargin] = useState(0)
  const hasRecords = records.length > 0
  const descending = direction === SortDirection.Descending

  useLayoutEffect(() => {
    const root = rootRef.current
    if (!root) return
    const container = findScrollParent(root)
    setScrollElement(container)
    if (!container) return

    const measure = () => {
      if (!listRef.current) return
      const top =
        listRef.current.getBoundingClientRect().top -
        container.getBoundingClientRect().top +
        container.scrollTop
      setScrollMargin((current) => (current === top ? current : top))
    }
    measure()

    window.addEventListener('resize', measure)
    const observer =
      typeof ResizeObserver === 'undefined'
        ? undefined
        : new ResizeObserver(measure)
    observer?.observe(container.firstElementChild ?? container)
    return () => {
      window.removeEventListener('resize', measure)
      observer?.disconnect()
    }
  }, [hasRecords])

  // eslint-disable-next-line react-hooks/incompatible-library -- the rule flags TanStack Virtual as compiler-incompatible; this component opts out of the compiler with 'use no memo', so there is no cached instance to go stale
  const virtualizer = useVirtualizer({
    count: records.length,
    getScrollElement: () => scrollElement,
    estimateSize: () => RowHeight,
    overscan: 8,
    scrollMargin,
    initialRect: InitialRect,
  })

  const items = virtualizer.getVirtualItems()
  const lastIndex = items.length > 0 ? items[items.length - 1].index : -1

  // Not after a failure: retrying on every render would loop. The footer offers
  // a retry instead.
  useEffect(() => {
    if (
      hasMore &&
      !isLoadingMore &&
      !isError &&
      lastIndex >= records.length - LoadAhead
    ) {
      onLoadMore()
    }
  }, [hasMore, isLoadingMore, isError, lastIndex, records.length, onLoadMore])

  return (
    <div ref={rootRef}>
      <div className={styles.listHeader}>
        <Input.Search
          allowClear
          aria-label="Filter by name or key"
          placeholder="Filter by name or key"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
        />
        <Space.Compact className={styles.sortControl}>
          <Select
            aria-label="Sort by"
            value={sort}
            onChange={onSortChange}
            popupMatchSelectWidth={false}
            style={{ minWidth: 140 }}
            options={[
              { value: FollowSort.LastChanged, label: 'Last changed' },
              { value: FollowSort.Followed, label: 'Date followed' },
            ]}
          />
          <Tooltip title={descending ? 'Newest first' : 'Oldest first'}>
            <Button
              aria-label={
                descending
                  ? 'Newest first. Switch to oldest first'
                  : 'Oldest first. Switch to newest first'
              }
              icon={
                descending ? (
                  <SortDescendingOutlined />
                ) : (
                  <SortAscendingOutlined />
                )
              }
              onClick={() =>
                onDirectionChange(
                  descending
                    ? SortDirection.Ascending
                    : SortDirection.Descending,
                )
              }
            />
          </Tooltip>
        </Space.Compact>
      </div>
      {isLoading ? (
        <Spin style={{ display: 'block', padding: 24 }} />
      ) : isError && !hasRecords ? (
        <Alert type="error" title="Could not load what you follow." />
      ) : !hasRecords ? (
        <div className={styles.listFooter}>Nothing matches.</div>
      ) : (
        <div
          ref={listRef}
          role="list"
          aria-label="Records you follow"
          style={{ height: virtualizer.getTotalSize(), position: 'relative' }}
        >
          {items.map((item) => {
            const record = records[item.index]
            return (
              <div
                key={recordId(record)}
                role="listitem"
                aria-setsize={totalCount}
                aria-posinset={item.index + 1}
                data-index={item.index}
                ref={virtualizer.measureElement}
                style={{
                  position: 'absolute',
                  top: 0,
                  left: 0,
                  width: '100%',
                  transform: `translateY(${item.start - scrollMargin}px)`,
                }}
              >
                <FollowedRecordRow
                  record={record}
                  typeName={typeName(record.ownerType)}
                  selected={recordId(record) === selectedId}
                  onSelect={onSelect}
                />
              </div>
            )
          })}
        </div>
      )}
      {hasRecords ? (
        <div className={styles.listFooter} aria-live="polite">
          {isLoadingMore ? (
            'Loading more…'
          ) : isError ? (
            <>
              Could not load more.{' '}
              <Button type="link" size="small" onClick={onLoadMore}>
                Try again
              </Button>
            </>
          ) : (
            `${records.length} of ${totalCount} shown`
          )}
        </div>
      ) : null}
    </div>
  )
}

export default FollowedRecordsList
