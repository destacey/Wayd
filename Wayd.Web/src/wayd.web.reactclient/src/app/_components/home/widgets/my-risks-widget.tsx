'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { RiskListDto } from '@/src/services/wayd-api'
import { useGetMyRisksQuery } from '@/src/store/features/planning/risks-api'
import {
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
  todayCalendarDate,
} from '@/src/utils'
import { Typography } from 'antd'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import { useHomeContext } from '../use-home-context'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeWidgetProps, widgetAnchorId } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** Whether a risk's follow-up falls today or earlier. */
const isFollowUpDue = (risk: RiskListDto, today: CalendarDate) =>
  !!risk.followUpDate && compareCalendarDates(risk.followUpDate, today) <= 0

/** How many of the risks have a follow-up due today or already past. */
export const countFollowUpsDue = (
  risks: RiskListDto[],
  today: CalendarDate,
): number => risks.filter((r) => isFollowUpDue(r, today)).length

/** Soonest follow-up first, risks with none after them, then by summary. */
export const sortByFollowUp = (risks: RiskListDto[]): RiskListDto[] =>
  [...risks].sort((a, b) => {
    if (a.followUpDate && b.followUpDate) {
      const byDate = compareCalendarDates(a.followUpDate, b.followUpDate)
      if (byDate !== 0) return byDate
    } else if (a.followUpDate || b.followUpDate) {
      return a.followUpDate ? -1 : 1
    }
    return caseInsensitiveCompare(a.summary, b.summary)
  })

/**
 * The query both the widget and its tile read; RTK Query shares the request.
 * The username is only the cache key — the API answers for the caller — so the
 * request never waits on it. Skipped when the home context says no open risk
 * is assigned to the caller.
 */
const useMyRisks = () => {
  const { user } = useAuth()
  const { context } = useHomeContext()
  return useGetMyRisksQuery(user?.username ?? '', {
    skip: context?.assignedRiskCount === 0,
  })
}

const FollowUp: FC<{ risk: RiskListDto; today: CalendarDate }> = ({
  risk,
  today,
}) => {
  if (!risk.followUpDate) return null
  const comparison = compareCalendarDates(risk.followUpDate, today)
  const label = `Follow-up ${formatCalendarDate(risk.followUpDate)}`
  if (comparison < 0) return <Text type="danger">{label} (past due)</Text>
  if (comparison === 0) return <Text type="warning">{label} (today)</Text>
  return <Text type="secondary">{label}</Text>
}

/** The open risks assigned to the caller, with when each needs following up. */
const MyRisksWidget: FC<HomeWidgetProps> = ({ title }) => {
  const { data: risks, isLoading, isError } = useMyRisks()
  const today = todayCalendarDate()

  const body = () => {
    if (isError) return <WidgetError message="Could not load your risks." />
    if (!risks || risks.length === 0) {
      return (
        <WidgetEmpty message="No open risks are assigned to you. They appear here when someone assigns you one." />
      )
    }
    return (
      <WaydList size="small">
        {sortByFollowUp(risks).map((risk) => (
          <WaydList.Item key={risk.key} className={styles.listItem}>
            <div className={styles.listItemBody}>
              <EntityLink href={`/planning/risks/${risk.key}`}>
                {risk.summary}
              </EntityLink>
              <span className={styles.listItemMeta}>
                <Text type="secondary">{risk.exposure} exposure</Text>
                <FollowUp risk={risk} today={today} />
              </span>
            </div>
          </WaydList.Item>
        ))}
      </WaydList>
    )
  }

  return (
    <WidgetCard title={title} loading={isLoading}>
      {body()}
    </WidgetCard>
  )
}

/** Risks assigned to the caller whose follow-up is due today or past. */
export const RiskFollowUpsTile: FC = () => {
  const { data: risks, isLoading, isError } = useMyRisks()
  const value = isError
    ? undefined
    : countFollowUpsDue(risks ?? [], todayCalendarDate())
  return (
    <AttentionTile
      title="Risk follow-ups"
      value={value}
      tooltip="Open risks assigned to you whose follow-up date is today or past."
      href={`#${widgetAnchorId('my-risks')}`}
      alert="warning"
      loading={isLoading}
    />
  )
}

export default MyRisksWidget
