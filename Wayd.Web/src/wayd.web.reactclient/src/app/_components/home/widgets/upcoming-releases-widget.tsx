'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import {
  NavigationDto,
  StatusNavigationDto,
  TeamProductReleasesDto,
} from '@/src/services/api/wayd.schemas'
import { useGetMyUpcomingReleasesQuery } from '@/src/store/features/user-management/me-api'
import {
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
} from '@/src/utils'
import { Flex, Tag, Typography } from 'antd'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeTileProps, HomeWidgetProps } from '../widget-registry'
import {
  daysOf,
  daysSetting,
  rowsToShow,
  WidgetSetting,
} from '../widget-settings'
import styles from '../home.module.css'

const { Text } = Typography

/** The most releases and versions the widget lists; past it, it says how many more there are. */
export const UPCOMING_RELEASES_LIMIT = 5

/** The setting for how far ahead a target date puts a release or version on the list. */
export const UPCOMING_WINDOW_SETTING = 'upcoming-window'

/** How far ahead a target date puts a release or version on the list until the user picks another window. */
export const UPCOMING_RELEASES_DAYS = 14

/** What the user can change about the widget, beyond its rows and empty display. */
export const upcomingReleasesSettings: WidgetSetting[] = [
  daysSetting(
    UPCOMING_WINDOW_SETTING,
    'List what is targeted within',
    [14, 30, 60],
    UPCOMING_RELEASES_DAYS,
  ),
]

const VERSIONS_HREF = '/product-management/versions'

/** One row of the widget: a release or a version, as the widget lists them. */
export interface UpcomingDelivery {
  kind: 'release' | 'version'
  id: string
  key: number
  label: string
  href: string
  targetDate: CalendarDate
  isPastTarget: boolean
  status: StatusNavigationDto
  /** The product names shown beside it. */
  products: NavigationDto[]
}

/** A heading and the rows under it: everything past target, or one target date. */
export interface UpcomingDeliveryGroup {
  key: string
  heading: string
  rows: UpcomingDelivery[]
}

const releaseLabel = (version: string, name?: string | null) =>
  name ? `${version} · ${name}` : version

/**
 * The releases and versions as one list, past target first, then by target
 * date, a release before a version on the same day, then by key.
 */
export const upcomingDeliveries = (
  schedule: TeamProductReleasesDto | undefined,
): UpcomingDelivery[] => {
  const releases: UpcomingDelivery[] = (schedule?.releases ?? []).map((r) => ({
    kind: 'release',
    id: r.id,
    key: r.key,
    label: releaseLabel(r.version, r.name),
    href: `/product-management/releases/${r.key}`,
    targetDate: r.targetDate,
    isPastTarget: r.isPastTarget,
    status: r.status,
    // A release spanning product lines names none; the team products it reaches say whose it is.
    products: r.product ? [r.product] : r.teamProducts,
  }))
  const versions: UpcomingDelivery[] = (schedule?.versions ?? []).map((v) => ({
    kind: 'version',
    id: v.id,
    key: v.key,
    label: `${v.product.name} ${releaseLabel(v.number, v.name)}`,
    href: `/product-management/versions/${v.key}`,
    targetDate: v.targetDate,
    isPastTarget: v.isPastTarget,
    status: v.status,
    products: [],
  }))
  return [...releases, ...versions].sort(
    (a, b) =>
      Number(b.isPastTarget) - Number(a.isPastTarget) ||
      compareCalendarDates(a.targetDate, b.targetDate) ||
      (a.kind === b.kind ? 0 : a.kind === 'release' ? -1 : 1) ||
      a.key - b.key,
  )
}

/** The rows under a "Past target" heading, then one heading per target date. */
export const groupByTargetDate = (
  rows: UpcomingDelivery[],
): UpcomingDeliveryGroup[] => {
  const groups: UpcomingDeliveryGroup[] = []
  for (const row of rows) {
    const key = row.isPastTarget ? 'past-target' : row.targetDate
    const last = groups[groups.length - 1]
    if (last?.key === key) {
      last.rows.push(row)
    } else {
      groups.push({
        key,
        heading: row.isPastTarget
          ? 'Past target'
          : formatCalendarDate(row.targetDate, 'ddd, MMM D'),
        rows: [row],
      })
    }
  }
  return groups
}

const DeliveryRow: FC<{ row: UpcomingDelivery }> = ({ row }) => (
  <WaydList.Item className={styles.listItem}>
    <div className={styles.listItemBody}>
      <EntityLink href={row.href}>{row.label}</EntityLink>
      <span className={styles.listItemMeta}>
        <Tag variant="outlined">
          {row.kind === 'release' ? 'Release' : 'Version'}
        </Tag>
        <WorkflowStatusTag
          name={row.status.name}
          category={row.status.category}
        />
        {row.isPastTarget ? (
          <Text type="danger">Target {formatCalendarDate(row.targetDate)}</Text>
        ) : null}
        {row.products.length > 0 ? (
          <Text type="secondary">
            {row.products.map((p) => p.name).join(', ')}
          </Text>
        ) : null}
      </span>
    </div>
  </WaydList.Item>
)

/**
 * The releases and versions targeted within the chosen window on the products
 * the caller's teams work on, and those past target, grouped by target date
 * with past target first.
 */
const UpcomingReleasesWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, UPCOMING_RELEASES_LIMIT)
  const days = daysOf(settings, UPCOMING_WINDOW_SETTING, UPCOMING_RELEASES_DAYS)
  const query = useGetMyUpcomingReleasesQuery(days)
  // While a changed window loads, RTK Query keeps the last window's result in `data`; listing it would show
  // what the new window leaves out, or call an empty window empty before it has loaded.
  const isChanging = !!query.isFetching && query.currentData === undefined
  const isLoading = query.isLoading || isChanging
  const isError = query.isError
  const deliveries = upcomingDeliveries(isChanging ? undefined : query.data)

  const body = () => {
    if (isError)
      return <WidgetError message="Could not load your products' releases." />
    if (deliveries.length === 0) {
      return (
        <WidgetEmpty
          message={`Nothing is targeted to ship in the next ${days} days on your teams' products. A release or version appears here once its target date falls within that window, and stays while it is past target.`}
        />
      )
    }
    const more = deliveries.length - rows
    return (
      <>
        <Flex vertical gap={8}>
          {groupByTargetDate(deliveries.slice(0, rows)).map((group) => (
            <Flex vertical gap={0} key={group.key}>
              <Text
                strong
                type={group.key === 'past-target' ? 'danger' : undefined}
              >
                {group.heading}
              </Text>
              <WaydList size="small">
                {group.rows.map((row) => (
                  <DeliveryRow key={`${row.kind}-${row.id}`} row={row} />
                ))}
              </WaydList>
            </Flex>
          ))}
        </Flex>
        {more > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            And {more} more.
          </Text>
        ) : null}
      </>
    )
  }

  return (
    <WidgetCard
      title={title}
      viewAllHref={VERSIONS_HREF}
      loading={isLoading}
      failed={isError}
      empty={deliveries.length === 0}
      emptySummary="Nothing due to ship"
    >
      {body()}
    </WidgetCard>
  )
}

/** Versions past their target date on the products the caller's teams work on. */
export const VersionsPastTargetTile: FC<HomeTileProps> = ({ title }) => {
  // Past target does not depend on the window, so the tile reads the default one the widget most often shares.
  const {
    data: schedule,
    isLoading,
    isError,
  } = useGetMyUpcomingReleasesQuery(UPCOMING_RELEASES_DAYS)
  return (
    <AttentionTile
      title={title}
      value={
        isError
          ? undefined
          : (schedule?.versions ?? []).filter((v) => v.isPastTarget).length
      }
      tooltip="Versions not yet released or withdrawn whose target date has passed, on the products your teams work on."
      href={VERSIONS_HREF}
      alert="warning"
      loading={isLoading}
      context="on your products"
    />
  )
}

export default UpcomingReleasesWidget
