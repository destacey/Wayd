'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { RoadmapListDto, RoadmapState } from '@/src/services/wayd-api'
import { useGetRoadmapsQuery } from '@/src/store/features/planning/roadmaps-api'
import {
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
  todayCalendarDate,
} from '@/src/utils'
import { Tag, Typography } from 'antd'
import { FC } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { rowsToShow } from '../widget-settings'
import { HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** The most roadmaps the widget lists; past it, it says how many more there are. */
export const MY_ROADMAPS_LIMIT = 5

const ROADMAPS_HREF = '/planning/roadmaps'

/** The caller's roadmaps by start date, ties by name, so the one under way longest comes first. */
export const sortRoadmaps = (roadmaps: RoadmapListDto[]): RoadmapListDto[] =>
  [...roadmaps].sort(
    (a, b) =>
      compareCalendarDates(a.start, b.start) ||
      caseInsensitiveCompare(a.name, b.name),
  )

const DateRange: FC<{ roadmap: RoadmapListDto; today: CalendarDate }> = ({
  roadmap,
  today,
}) => {
  const label = `${formatCalendarDate(roadmap.start)} – ${formatCalendarDate(roadmap.end)}`
  // An active roadmap past its end is one its owner should extend or archive.
  if (compareCalendarDates(roadmap.end, today) < 0)
    return <Text type="danger">{label} (past end date)</Text>
  return <Text type="secondary">{label}</Text>
}

/** The active roadmaps the caller is an owner of, earliest start first. */
const MyRoadmapsWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, MY_ROADMAPS_LIMIT)
  const {
    data: roadmaps,
    isLoading,
    isError,
  } = useGetRoadmapsQuery({ state: [RoadmapState.Active], ownedByMe: true })
  const today = todayCalendarDate()

  const body = () => {
    if (isError) return <WidgetError message="Could not load your roadmaps." />
    if (!roadmaps || roadmaps.length === 0) {
      return (
        <WidgetEmpty message="You own no active roadmaps. They appear here when a roadmap names you as an owner." />
      )
    }
    const more = roadmaps.length - rows
    return (
      <>
        <WaydList size="small">
          {sortRoadmaps(roadmaps)
            .slice(0, rows)
            .map((roadmap) => (
              <WaydList.Item key={roadmap.id} className={styles.listItem}>
                <div className={styles.listItemBody}>
                  <EntityLink href={`/planning/roadmaps/${roadmap.key}`}>
                    {roadmap.name}
                  </EntityLink>
                  <span className={styles.listItemMeta}>
                    <DateRange roadmap={roadmap} today={today} />
                    <Tag>{roadmap.visibility.name}</Tag>
                  </span>
                </div>
              </WaydList.Item>
            ))}
        </WaydList>
        {more > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            And {more} more.
          </Text>
        ) : null}
      </>
    )
  }

  return (
    <WidgetCard title={title} viewAllHref={ROADMAPS_HREF} loading={isLoading}>
      {body()}
    </WidgetCard>
  )
}

export default MyRoadmapsWidget
