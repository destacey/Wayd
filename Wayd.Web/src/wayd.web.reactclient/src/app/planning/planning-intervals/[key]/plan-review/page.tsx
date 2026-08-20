'use client'

import { useDocumentTitle } from '@/src/hooks'
import { PlanningIntervalTeamResponse } from '@/src/services/wayd-api'
import { use, useEffect, useMemo, useState } from 'react'
import { Alert, Card, Tag } from 'antd'
import TeamPlanReview from './team-plan-review'
import { notFound } from 'next/navigation'
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

  // Tab the user/URL has selected. `undefined` is the "haven't checked yet"
  // sentinel — important because Next.js's client-side navigation commits the
  // URL hash AFTER the first render phase, so window.location.hash returns ""
  // during render even when the URL bar shows "/plan-review#juice". We read
  // the hash in an effect (post-commit) and gate the URL-mirroring effect
  // until that read has happened, otherwise mirror would clobber the incoming
  // hash with the first-team fallback on render 1.
  const [selectedTab, setSelectedTab] = useState<string | null | undefined>(
    undefined,
  )
  useEffect(() => {
    // Legitimate mount-time read of window.location.hash. Lint flags this as
    // "setState in effect" but it's the documented pattern for reading
    // external state on mount when render-phase reads aren't available
    // (see comment above on Next.js's hash-commit timing).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSelectedTab(window.location.hash.slice(1) || null)
  }, [])

  // Derive activeTab: hash/user selection wins; otherwise the first team.
  // While selectedTab is undefined (pre-mount-effect), keep activeTab empty
  // so the mirror effect doesn't write the wrong hash.
  const activeTab =
    selectedTab === undefined
      ? ''
      : selectedTab || (teams.length > 0 ? teams[0]?.code.toLowerCase() : '')

  // Mirror activeTab back into the URL hash whenever it changes. We use
  // history.replaceState rather than router.push/replace('#hash') because
  // next/navigation's bare-hash forms append instead of replacing the
  // fragment, producing doubled hashes like ".../plan-review#data#core".
  useEffect(() => {
    if (selectedTab === undefined) return
    if (!activeTab || teams.length === 0) return
    const currentHash = window.location.hash.slice(1)
    if (currentHash === activeTab) return

    const newUrl = `${window.location.pathname}${window.location.search}#${activeTab}`
    window.history.replaceState(null, '', newUrl)
  }, [activeTab, teams, selectedTab])

  // Handle hash change events (back/forward navigation, manual edits)
  useEffect(() => {
    const handleHashChange = () => {
      const hash = window.location.hash.slice(1)
      setSelectedTab(hash || null)
    }
    window.addEventListener('hashchange', handleHashChange)

    return () => {
      window.removeEventListener('hashchange', handleHashChange)
    }
  }, [])

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
        onTabChange={(key) => setSelectedTab(key)}
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

const PlanningIntervalPlanReviewPageWithAuthorization = authorizePage(
  PlanningIntervalPlanReviewPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalPlanReviewPageWithAuthorization
