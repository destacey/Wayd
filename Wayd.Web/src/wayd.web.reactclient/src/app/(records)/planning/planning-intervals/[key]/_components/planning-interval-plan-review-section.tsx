'use client'

import TeamPlanReview from './plan-review/team-plan-review'
import { WaydEmpty } from '@/src/components/common'
import {
  PlanningIntervalDetailsDto,
  PlanningIntervalTeamResponse,
} from '@/src/services/wayd-api'
import { useGetPlanningIntervalTeamsQuery } from '@/src/store/features/planning/planning-interval-api'
import { Alert, Card, Skeleton } from 'antd'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useMemo } from 'react'

export interface PlanningIntervalPlanReviewSectionProps {
  planningInterval: PlanningIntervalDetailsDto
  refreshPlanningInterval: () => void
}

/**
 * Each team's plan for the PI, one team at a time.
 *
 * The team is a second axis of state alongside the record's section, so it
 * lives in the URL as `?team=` — readable during render, unlike a hash, so the
 * first paint already shows the team the link named.
 */
const PlanningIntervalPlanReviewSection = ({
  planningInterval,
  refreshPlanningInterval,
}: PlanningIntervalPlanReviewSectionProps) => {
  const { data: teamData, isLoading } = useGetPlanningIntervalTeamsQuery(
    planningInterval.key,
  )

  const searchParams = useSearchParams()
  const pathname = usePathname()
  const router = useRouter()

  const teams = useMemo(
    () =>
      !teamData
        ? []
        : teamData
            .filter((t) => t.type === 'Team')
            .sort((a, b) => a.code.localeCompare(b.code)),
    [teamData],
  )

  const requestedTeam = searchParams.get('team')
  const activeTab =
    requestedTeam ?? (teams.length > 0 ? teams[0]?.code.toLowerCase() : '')

  const selectTeam = (code: string) => {
    const params = new URLSearchParams(searchParams.toString())
    params.set('team', code)
    // replace, not push: Back returns to where the user came from rather than
    // stepping back through every team they looked at.
    router.replace(`${pathname}?${params.toString()}`, { scroll: false })
  }

  if (isLoading) return <Skeleton active />
  if (teams.length === 0)
    return <WaydEmpty message="No teams found for this PI" />

  const tabs = teams.map((team) => ({
    key: team.code.toLowerCase(),
    tab: team.code,
  }))

  const activeTeam: PlanningIntervalTeamResponse | undefined = teams.find(
    (t) => t.code.toLowerCase() === activeTab,
  )

  return (
    <Card
      style={{ width: '100%' }}
      tabList={tabs}
      activeTabKey={activeTab}
      onTabChange={selectTeam}
    >
      {!activeTeam ? (
        <Alert title="Please select a valid team." type="error" />
      ) : (
        <TeamPlanReview
          planningInterval={planningInterval}
          team={activeTeam}
          refreshPlanningInterval={refreshPlanningInterval}
        />
      )}
    </Card>
  )
}

export default PlanningIntervalPlanReviewSection
