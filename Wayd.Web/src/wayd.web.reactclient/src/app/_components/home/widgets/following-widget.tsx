'use client'

import FollowedRecordRow from '@/src/app/account/following/_components/followed-record-row'
import { recordHref } from '@/src/components/common/record/record-href'
import {
  recordId,
  typeLabel,
} from '@/src/components/common/record/record-identity'
import { FollowSort, SortDirection } from '@/src/services/wayd-api'
import { useGetMyFollowsInfiniteQuery } from '@/src/store/features/common/follows-api'
import { Typography } from 'antd'
import { FC } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { MAX_ROWS, rowsToShow } from '../widget-settings'
import { HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** How many followed records the widget lists; the Following page has the rest. */
export const FOLLOWING_COUNT = 8

/**
 * The records the caller follows, most recently changed first, in the
 * Following page's own rows — without its search, sort or paging, and each
 * row a link to the record rather than a selection.
 */
const FollowingWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rowCount = rowsToShow(settings, FOLLOWING_COUNT)
  // One page, of the most the setting offers: the widget is a glance, and the Following page pages the rest.
  const { data, isLoading, isError } = useGetMyFollowsInfiniteQuery({
    sort: FollowSort.LastChanged,
    direction: SortDirection.Descending,
    pageSize: MAX_ROWS,
  })
  const firstPage = data?.pages[0]
  // A record type the client has no page for yet cannot be a link, so it is left out.
  const rows = (firstPage?.items ?? [])
    .map((record) => ({ record, href: recordHref(record) }))
    .filter((row): row is typeof row & { href: string } => row.href !== null)
    .slice(0, rowCount)
  const more = (firstPage?.totalCount ?? 0) - rows.length

  const body = () => {
    if (isError) {
      return <WidgetError message="Could not load what you follow." />
    }
    if (rows.length === 0) {
      return (
        <WidgetEmpty message="You are not following anything. Select the eye at the top of a project, program or PI objective to follow it." />
      )
    }
    return (
      <>
        <div
          role="list"
          aria-label="Followed records"
          className={styles.flushRows}
        >
          {rows.map(({ record, href }) => (
            <div key={recordId(record)} role="listitem">
              <FollowedRecordRow
                record={record}
                typeName={typeLabel(record.ownerType)}
                href={href}
              />
            </div>
          ))}
        </div>
        {more > 0 ? (
          <Text type="secondary" className={styles.flushMore}>
            And {more} more.
          </Text>
        ) : null}
      </>
    )
  }

  return (
    <WidgetCard
      title={title}
      viewAllHref="/account/following"
      loading={isLoading}
      flush={!isError && rows.length > 0}
      failed={isError}
      empty={rows.length === 0}
      emptySummary="Not following anything"
    >
      {body()}
    </WidgetCard>
  )
}

export default FollowingWidget
