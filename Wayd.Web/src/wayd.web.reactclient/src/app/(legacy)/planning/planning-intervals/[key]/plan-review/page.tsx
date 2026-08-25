'use client'

import { useDocumentTitle } from '@/src/hooks'
import { PlanningIntervalTeamResponse } from '@/src/services/wayd-api'
import { Suspense, use, useMemo } from 'react'
import { Alert, Card, Tag } from 'antd'
import TeamPlanReview from './team-plan-review'
import {
  notFound,
  usePathname,
  useRouter,
  useSearchParams,
} from 'next/navigation'
import { WaydEmpty, PageTitle } from '@/src/components/common'
import PlanningIntervalPlanReviewLoading from './loading'
import { authorizePage } from '@/src/components/hoc'
import {
  useGetPlanningIntervalQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'

const PlanningIntervalPlanReviewPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const piKey = Number(key)

  useDocumentTitle('PI Plan Review')

  const {
    data: planningIntervalData,
    isLoading,
    error,
    refetch: refetchPlanningInterval,
  } = useGetPlanningIntervalQuery(piKey)

  const { data: teamData, isLoading: teamsIsLoading } =
    useGetPlanningIntervalTeamsQuery(piKey)

  const predictability = planningIntervalData?.predictability

  const teams = useMemo(
    () =>
      !teamData
        ? []
        : teamData
            .filter((t) => t.type === 'Team')
            .sort((a, b) => a.code.localeCompare(b.code)),
    [teamData],
  )

  // The selected team lives in the URL as ?team=, not a hash. A search param is
  // readable during render, so the first paint already shows the right team —
  // a hash is committed after render, which previously needed a sentinel, a
  // mount effect, a mirror effect and a hashchange listener to work around.
  const searchParams = useSearchParams()
  const pathname = usePathname()
  const router = useRouter()

  const requestedTeam = searchParams.get('team')
  const activeTab =
    requestedTeam ?? (teams.length > 0 ? teams[0]?.code.toLowerCase() : '')

  const selectTeam = (code: string) => {
    const params = new URLSearchParams(searchParams.toString())
    params.set('team', code)
    // replace, not push: Back returns to where the user came from rather than
    // stepping back through every team they looked at. scroll:false or the
    // router jumps to the top on each switch.
    router.replace(`${pathname}?${params.toString()}`, { scroll: false })
  }

  const tabs = teams?.map((team) => ({
    key: team.code.toLowerCase(),
    tab: team.code,
  }))

  const activeTeam: PlanningIntervalTeamResponse | undefined =
    !teams || teams.length === 0 || !activeTab
      ? undefined
      : teams?.find((t) => t.code.toLowerCase() === activeTab)

  if (!isLoading && !planningIntervalData) {
    return notFound()
  }
  if (isLoading || teamsIsLoading) return <PlanningIntervalPlanReviewLoading />
  if (!planningIntervalData) return null
  if (tabs?.length === 0)
    return <WaydEmpty message="No teams found for this PI" />

  const tabExists = tabs?.some((t) => t.key === activeTab)

  return (
    <>
      <PageTitle
        title="PI Plan Review"
        tags={
          predictability != null && (
            <Tag title="PI Predictability">{`${predictability}%`}</Tag>
          )
        }
      />
      <Card
        style={{ width: '100%' }}
        tabList={tabs}
        activeTabKey={activeTab}
        onTabChange={selectTeam}
      >
        {!tabExists ? (
          <Alert title="Please select a valid team." type="error" />
        ) : (
          <TeamPlanReview
            planningInterval={planningIntervalData}
            team={activeTeam!}
            refreshPlanningInterval={refetchPlanningInterval}
          />
        )}
      </Card>
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const PlanningIntervalPlanReviewPageWithSuspense = (props: {
  params: Promise<{ key: string }>
}) => (
  <Suspense fallback={<PlanningIntervalPlanReviewLoading />}>
    <PlanningIntervalPlanReviewPage {...props} />
  </Suspense>
)

const PlanningIntervalPlanReviewPageWithAuthorization = authorizePage(
  PlanningIntervalPlanReviewPageWithSuspense,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalPlanReviewPageWithAuthorization
