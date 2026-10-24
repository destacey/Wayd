'use client'

import { Alert, Grid, Spin } from 'antd'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { Suspense, useState } from 'react'
import { PageTitle, WaydEmpty } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDebounce, useDocumentTitle } from '@/src/hooks'
import { FollowedRecordDto, FollowSort } from '@/src/services/wayd-api'
import {
  useGetMyFollowCountsQuery,
  useGetMyFollowsInfiniteQuery,
} from '@/src/store/features/common/follows-api'
import FollowedRecordDetail from './_components/followed-record-detail'
import FollowedRecordsList from './_components/followed-records-list'
import FollowingFilterBar from './_components/following-filter-bar'
import {
  FollowingFilter,
  LowCountThreshold,
  recordId,
} from './_components/following-filters'
import FollowingRail from './_components/following-rail'
import styles from './following.module.css'

const { useBreakpoint } = Grid

/** The query parameter that opens one record's detail on a phone. */
const RecordParam = 'record'

const parseRecord = (value: string | null) => {
  const separator = value?.lastIndexOf(':') ?? -1
  return value && separator > 0
    ? {
        ownerType: value.slice(0, separator),
        ownerId: value.slice(separator + 1),
      }
    : undefined
}

const FollowingContent = () => {
  const { user } = useAuth()
  const screens = useBreakpoint()
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()

  // Before the first measurement every breakpoint reads false; treat that as wide
  // rather than flashing the phone layout on a desktop.
  const measured = Object.keys(screens).length > 0
  const isPhone = measured && !screens.md
  const isWide = !measured || !!screens.xl

  const [filter, setFilter] = useState<FollowingFilter>({})
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<FollowSort>(FollowSort.LastChanged)
  const [picked, setPicked] = useState<string>()
  const debouncedSearch = useDebounce(search.trim(), 300)

  const counts = useGetMyFollowCountsQuery(undefined, {
    skip: !user?.employeeId,
  })
  const list = useGetMyFollowsInfiniteQuery(
    {
      ownerType: filter.ownerType,
      view: filter.view,
      search: debouncedSearch || undefined,
      sort,
    },
    { skip: !user?.employeeId },
  )

  const records = list.data?.pages.flatMap((page) => page.items) ?? []
  const totalCount = list.data?.pages[0]?.totalCount ?? 0
  const typeName = (ownerType: string) =>
    counts.data?.types.find((t) => t.ownerType === ownerType)?.displayName ??
    'Record'

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

  const onSelect = (record: FollowedRecordDto) => {
    if (isPhone) {
      router.push(
        `${pathname}?${RecordParam}=${encodeURIComponent(recordId(record))}`,
      )
    } else {
      setPicked(recordId(record))
    }
  }

  const onUnfollowed = () => {
    if (isPhone) {
      router.back()
    } else {
      setPicked(undefined)
    }
  }

  const onFilterChange = (next: FollowingFilter) => {
    setFilter(next)
    setPicked(undefined)
  }

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

  const lowCount = counts.data.total < LowCountThreshold

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
        onSearchChange={setSearch}
        sort={sort}
        onSortChange={setSort}
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
        onBack={() => router.back()}
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
          onUnfollowed={onUnfollowed}
        />
      ) : (
        <WaydEmpty message="Select a record to see it here." />
      )}
    </div>
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
        {listPane}
        {detailPane}
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
      <div className={`${styles.panes} ${styles.twoPanes}`}>
        {listPane}
        {detailPane}
      </div>
    </>
  )
}

const FollowingPage = () => {
  useDocumentTitle('Following')

  return (
    <div className={styles.page}>
      <PageTitle
        title="Following"
        subtitle="Records you follow and can still see. Following records your interest; notifications come later."
      />
      <Suspense fallback={<Spin />}>
        <FollowingContent />
      </Suspense>
    </div>
  )
}

export default requireFeatureFlag(FollowingPage, 'following')
