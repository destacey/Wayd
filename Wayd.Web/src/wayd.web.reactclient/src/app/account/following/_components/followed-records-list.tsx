'use client'

import { Alert, Badge, Input, Select, Spin } from 'antd'
import { useWindowVirtualizer } from '@tanstack/react-virtual'
import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { LifecycleStatusTag } from '@/src/components/common'
import { FollowedRecordDto, FollowSort } from '@/src/services/wayd-api'
import styles from '../following.module.css'
import { healthBadgeStatus, healthLabel, recordId } from './following-filters'
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
  selectedId: string | undefined
  onSelect: (record: FollowedRecordDto) => void
}

const RowHeight = 64

/** When fewer than this many loaded rows remain below the window, the next page loads. */
const LoadAhead = 10

interface RowProps {
  record: FollowedRecordDto
  selected: boolean
  onSelect: (record: FollowedRecordDto) => void
}

const FollowedRecordRow = ({ record, selected, onSelect }: RowProps) => (
  <button
    type="button"
    role="option"
    aria-selected={selected}
    className={`${styles.row} ${selected ? styles.rowSelected : ''}`}
    onClick={() => onSelect(record)}
  >
    <Badge
      status={healthBadgeStatus(record.health)}
      aria-label={healthLabel(record.health)}
    />
    <span className={styles.rowBody}>
      <span className={styles.rowName}>{record.name}</span>
      <span className={styles.rowMeta}>
        {record.key}
        {record.parentName ? ` · ${record.parentName}` : ''}
        {' · '}
        {record.lastChangedOn
          ? `changed ${formatTimeAgo(record.lastChangedOn)}`
          : 'no changes yet'}
      </span>
    </span>
    <LifecycleStatusTag
      status={{
        id: 0,
        name: record.statusName,
        lifecycleCategory: record.lifecycle,
      }}
    />
  </button>
)

/**
 * The followed records, one row each. Selecting a row is the only thing a row
 * does; what can be done with a record lives in its detail.
 *
 * Virtualised against the window rather than a fixed-height box, so the list
 * scrolls with the page on every screen size, and the next page loads as the
 * end comes into view.
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
  selectedId,
  onSelect,
}: FollowedRecordsListProps) => {
  const listRef = useRef<HTMLDivElement>(null)
  // Where the list starts on the page, which the window virtualizer needs to
  // know which rows are in view. Measured after layout, and again on resize,
  // since the header above the list wraps differently at each width.
  const [scrollMargin, setScrollMargin] = useState(0)
  const hasRecords = records.length > 0

  useLayoutEffect(() => {
    const measure = () => {
      if (listRef.current) {
        setScrollMargin(
          listRef.current.getBoundingClientRect().top + window.scrollY,
        )
      }
    }
    measure()
    window.addEventListener('resize', measure)
    return () => window.removeEventListener('resize', measure)
  }, [hasRecords])

  const virtualizer = useWindowVirtualizer({
    count: records.length,
    estimateSize: () => RowHeight,
    overscan: 8,
    scrollMargin,
  })

  const items = virtualizer.getVirtualItems()
  const lastIndex = items.length > 0 ? items[items.length - 1].index : -1

  useEffect(() => {
    if (hasMore && !isLoadingMore && lastIndex >= records.length - LoadAhead) {
      onLoadMore()
    }
  }, [hasMore, isLoadingMore, lastIndex, records.length, onLoadMore])

  return (
    <div>
      <div className={styles.listHeader}>
        <Input.Search
          allowClear
          aria-label="Filter by name or key"
          placeholder={`Filter these ${totalCount}`}
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
        />
        <Select
          aria-label="Sort"
          value={sort}
          onChange={onSortChange}
          style={{ minWidth: 130 }}
          options={[
            { value: FollowSort.LastChanged, label: 'Changed' },
            { value: FollowSort.Followed, label: 'Followed' },
          ]}
        />
      </div>
      {isError ? (
        <Alert type="error" title="Could not load what you follow." />
      ) : isLoading ? (
        <Spin style={{ display: 'block', padding: 24 }} />
      ) : records.length === 0 ? (
        <div className={styles.listFooter}>Nothing matches.</div>
      ) : (
        <div
          ref={listRef}
          role="listbox"
          aria-label="Records you follow"
          style={{ height: virtualizer.getTotalSize(), position: 'relative' }}
        >
          {items.map((item) => {
            const record = records[item.index]
            return (
              <div
                key={recordId(record)}
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
                  selected={recordId(record) === selectedId}
                  onSelect={onSelect}
                />
              </div>
            )
          })}
        </div>
      )}
      <div className={styles.listFooter}>
        {isLoadingMore
          ? 'Loading more…'
          : `${records.length} of ${totalCount} shown`}
      </div>
    </div>
  )
}

export default FollowedRecordsList
