'use client'

import { Alert, Grid, Spin, Splitter } from 'antd'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import {
  CSSProperties,
  Suspense,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from 'react'
import { PageTitle, WaydEmpty } from '@/src/components/common'
import {
  recordId,
  typeLabel,
} from '@/src/components/common/record/record-identity'
import useAuth from '@/src/components/contexts/auth'
import { requireFeatureFlag } from '@/src/components/hoc'
import {
  useDebounce,
  useDocumentTitle,
  useLocalStorageState,
} from '@/src/hooks'
import {
  FollowedRecordDto,
  FollowSort,
  SortDirection,
} from '@/src/services/wayd-api'
import {
  FollowOwnerTypes,
  useGetMyFollowCountsQuery,
  useGetMyFollowsInfiniteQuery,
} from '@/src/store/features/common/follows-api'
import { findScrollParent } from '@/src/utils/scroll-parent'
import FollowedRecordDetail from './_components/followed-record-detail'
import FollowedRecordsList from './_components/followed-records-list'
import FollowingFilterBar from './_components/following-filter-bar'
import {
  FollowingFilter,
  LowCountThreshold,
} from './_components/following-filters'
import FollowingRail from './_components/following-rail'
import styles from './following.module.css'

const { useBreakpoint } = Grid

/** The query parameter that opens one record's detail on a phone. */
const RecordParam = 'record'

/** The list's share of the width it splits with the detail, as a percentage. */
const DefaultListPercent = 40

/** Clamp a share so a bad or extreme value cannot hide either pane. */
const listShare = (percent: number) =>
  Number.isFinite(percent)
    ? Math.min(Math.max(percent, 25), 70)
    : DefaultListPercent

const knownOwnerTypes: readonly string[] = Object.values(FollowOwnerTypes)

/** Reads `ownerType:id` from the URL, refusing anything that names no followable record. */
export const parseRecord = (value: string | null) => {
  const separator = value?.lastIndexOf(':') ?? -1
  if (!value || separator <= 0) return undefined
  const ownerType = value.slice(0, separator)
  const ownerId = value.slice(separator + 1)
  return ownerId && knownOwnerTypes.includes(ownerType)
    ? { ownerType, ownerId }
    : undefined
}

const FollowingContent = () => {
  const { user } = useAuth()
  const screens = useBreakpoint()
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const rootRef = useRef<HTMLDivElement>(null)
  // Whether this page put the record in the URL, so going back stays on it.
  const pushedRecord = useRef(false)
  // Where the phone list was scrolled to before a record opened over it.
  const listScrollTop = useRef<number>(undefined)

  // Before the first measurement every breakpoint reads false; treat that as wide
  // rather than flashing the phone layout on a desktop.
  const measured = Object.keys(screens).length > 0
  const isPhone = measured && !screens.md
  const isWide = !measured || !!screens.xl

  const [filter, setFilter] = useState<FollowingFilter>({})
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<FollowSort>(FollowSort.LastChanged)
  const [direction, setDirection] = useState<SortDirection>(
    SortDirection.Descending,
  )
  const [picked, setPicked] = useState<string>()
  const [focusDetail, setFocusDetail] = useState(false)
  const [viewHeight, setViewHeight] = useState<number>()
  const [listPercent, setListPercent] = useLocalStorageState(
    'wayd.following.listPercent',
    DefaultListPercent,
  )
  const debouncedSearch = useDebounce(search.trim(), 300)

  const counts = useGetMyFollowCountsQuery(undefined, {
    skip: !user?.employeeId,
  })
  // With few records the filter controls are hidden, so no filter may apply:
  // one left over from before would narrow the list with nothing to clear it.
  const lowCount = (counts.data?.total ?? 0) < LowCountThreshold
  const activeFilter: FollowingFilter = lowCount ? {} : filter
  const list = useGetMyFollowsInfiniteQuery(
    {
      ownerType: activeFilter.ownerType,
      view: activeFilter.view,
      search: debouncedSearch || undefined,
      sort,
      direction,
    },
    { skip: !user?.employeeId },
  )

  const records = list.data?.pages.flatMap((page) => page.items) ?? []
  const totalCount = list.data?.pages[0]?.totalCount ?? 0
  const typeName = (ownerType: string) =>
    typeLabel(
      ownerType,
      counts.data?.types.find((t) => t.ownerType === ownerType)?.displayName,
    )

  // On a phone the selection lives in the URL, so the browser's back button
  // returns to the list. Elsewhere it is local state, falling back to the first
  // row so the detail pane is never empty beside a list.
  const phoneRecord = isPhone
    ? parseRecord(searchParams.get(RecordParam))
    : undefined
  const pickedRecord = picked
    ? records.find((r) => recordId(r) === picked)
    : undefined
  const selected = isPhone
    ? phoneRecord
    : (() => {
        const record = pickedRecord ?? records[0]
        return record
          ? { ownerType: record.ownerType, ownerId: record.id }
          : undefined
      })()
  const selectedId = selected
    ? `${selected.ownerType}:${selected.ownerId}`
    : undefined
  const showingList = !(isPhone && selected)

  // Sticky panes may be no taller than the area the page scrolls in.
  useLayoutEffect(() => {
    const container = rootRef.current && findScrollParent(rootRef.current)
    if (!container) return
    const measure = () => setViewHeight(container.clientHeight)
    measure()
    const observer =
      typeof ResizeObserver === 'undefined'
        ? undefined
        : new ResizeObserver(measure)
    observer?.observe(container)
    return () => observer?.disconnect()
  }, [])

  // Back on the phone list, return to where it was left.
  useEffect(() => {
    const container = rootRef.current && findScrollParent(rootRef.current)
    if (showingList && container && listScrollTop.current !== undefined) {
      container.scrollTo({ top: listScrollTop.current })
      listScrollTop.current = undefined
    }
  }, [showingList])

  const onSelect = (record: FollowedRecordDto) => {
    if (isPhone) {
      const container = rootRef.current && findScrollParent(rootRef.current)
      listScrollTop.current = container?.scrollTop
      pushedRecord.current = true
      router.push(
        `${pathname}?${RecordParam}=${encodeURIComponent(recordId(record))}`,
      )
    } else {
      setPicked(recordId(record))
    }
  }

  // Back to the list: the browser's history when this page opened the record,
  // else the list itself, so a shared link or refresh never leaves the page.
  const backToList = () => {
    if (pushedRecord.current) {
      pushedRecord.current = false
      router.back()
    } else {
      router.replace(pathname)
    }
  }

  // Beside the list, move to the record next to the one unfollowed rather than
  // the top row, which may be scrolled out of view.
  const onUnfollowed = () => {
    if (isPhone) {
      backToList()
      return
    }
    const index = records.findIndex((r) => recordId(r) === selectedId)
    const neighbour = records[index + 1] ?? records[index - 1]
    setPicked(neighbour ? recordId(neighbour) : undefined)
    setFocusDetail(true)
  }

  // A new filter, search, sort or order gives a new list, in which the old
  // selection may not be loaded; start again from its first row.
  const onFilterChange = (next: FollowingFilter) => {
    setFilter(next)
    setPicked(undefined)
  }
  const onSearchChange = (next: string) => {
    setSearch(next)
    setPicked(undefined)
  }
  const onSortChange = (next: FollowSort) => {
    setSort(next)
    setPicked(undefined)
  }
  const onDirectionChange = (next: SortDirection) => {
    setDirection(next)
    setPicked(undefined)
  }

  const content = (() => {
    if (!user?.employeeId) {
      return (
        <WaydEmpty message="Following needs your account to be linked to an employee. Ask an administrator to link it in Settings → Users." />
      )
    }

    if (counts.isError) {
      return <Alert type="error" title="Could not load what you follow." />
    }

    if (counts.isLoading || !counts.data) {
      return <Spin />
    }

    if (counts.data.total === 0) {
      return (
        <WaydEmpty message="You are not following anything yet. Following keeps the projects, programs and planning interval objectives you care about in one place. Open one and select the eye at the top of its page to follow it. Following grants no access: you can follow only what you can already see." />
      )
    }

    const listPane = (
      <div className={styles.pane}>
        <FollowedRecordsList
          records={records}
          totalCount={totalCount}
          isLoading={list.isLoading}
          isError={list.isError}
          hasMore={!!list.hasNextPage}
          isLoadingMore={list.isFetchingNextPage}
          onLoadMore={() => list.fetchNextPage()}
          search={search}
          onSearchChange={onSearchChange}
          sort={sort}
          onSortChange={onSortChange}
          direction={direction}
          onDirectionChange={onDirectionChange}
          typeName={typeName}
          selectedId={selectedId}
          onSelect={onSelect}
        />
      </div>
    )

    // <768px: list and detail are two views, never stacked panes.
    if (isPhone) {
      return selected ? (
        <FollowedRecordDetail
          key={selectedId}
          ownerType={selected.ownerType}
          ownerId={selected.ownerId}
          typeName={typeName(selected.ownerType)}
          asView
          onBack={backToList}
          onUnfollowed={onUnfollowed}
        />
      ) : (
        <>
          {lowCount ? null : (
            <FollowingFilterBar
              counts={counts.data}
              filter={filter}
              onChange={onFilterChange}
            />
          )}
          {listPane}
        </>
      )
    }

    const detailPane = (
      <div className={`${styles.pane} ${styles.sticky}`}>
        {selected ? (
          <FollowedRecordDetail
            key={selectedId}
            ownerType={selected.ownerType}
            ownerId={selected.ownerId}
            typeName={typeName(selected.ownerType)}
            autoFocus={focusDetail}
            onUnfollowed={onUnfollowed}
          />
        ) : (
          <WaydEmpty message="Select a record to see it here." />
        )}
      </div>
    )

    // A draggable divider between list and detail; the chosen share is kept for next time.
    const split = (
      <Splitter
        className={styles.split}
        onResizeEnd={([listSize, detailSize]) => {
          if (listSize > 0 && detailSize > 0) {
            setListPercent(
              listShare(Math.round((listSize / (listSize + detailSize)) * 100)),
            )
          }
        }}
      >
        {/* antd panels scroll by default, which would stop the detail sticking to the page. */}
        <Splitter.Panel
          defaultSize={`${listShare(listPercent)}%`}
          min={280}
          style={{ overflow: 'visible' }}
        >
          {listPane}
        </Splitter.Panel>
        <Splitter.Panel min={360} style={{ overflow: 'visible' }}>
          {detailPane}
        </Splitter.Panel>
      </Splitter>
    )

    // ≥1200px with enough records: rail, list, detail.
    if (isWide && !lowCount) {
      return (
        <div className={`${styles.panes} ${styles.threePanes}`}>
          <div className={`${styles.pane} ${styles.sticky}`}>
            <FollowingRail
              counts={counts.data}
              filter={filter}
              onChange={onFilterChange}
            />
          </div>
          {split}
        </div>
      )
    }

    // 768–1199px, or any width in low-count mode: list and detail side by side.
    return (
      <>
        {lowCount ? null : (
          <FollowingFilterBar
            counts={counts.data}
            filter={filter}
            onChange={onFilterChange}
          />
        )}
        {split}
      </>
    )
  })()

  return (
    <div
      ref={rootRef}
      className={styles.content}
      style={
        viewHeight
          ? ({ '--following-view-height': `${viewHeight}px` } as CSSProperties)
          : undefined
      }
    >
      {content}
    </div>
  )
}

const FollowingPage = () => {
  useDocumentTitle('Following')

  return (
    <div className={styles.page}>
      <PageTitle
        title="Following"
        subtitle="Records you follow and can still see. You are told about their changes in Notifications, under the bell."
      />
      <Suspense fallback={<Spin />}>
        <FollowingContent />
      </Suspense>
    </div>
  )
}

export default requireFeatureFlag(FollowingPage, 'following')
