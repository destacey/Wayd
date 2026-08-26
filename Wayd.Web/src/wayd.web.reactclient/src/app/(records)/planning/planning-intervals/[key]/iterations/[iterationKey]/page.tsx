'use client'

import { IconMenu } from '@/src/components/common'
import {
  IterationStateTag,
  SprintBacklogGrid,
} from '@/src/components/common/planning'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import { authorizePage } from '@/src/components/hoc'
import { IterationState } from '@/src/components/types'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetPlanningIntervalIterationBacklogQuery,
  useGetPlanningIntervalIterationQuery,
  useGetPlanningIntervalIterationsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import { SwapOutlined } from '@ant-design/icons'
import { Flex } from 'antd'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { ReactNode, Suspense, use, useState } from 'react'
import PlanningIntervalIterationLoading from './loading'
import { PlanningIntervalIterationOverview } from './_components'
import PlanningIntervalIterationFacts from './_components/planning-interval-iteration-facts'

enum IterationSections {
  Overview = 'overview',
  Backlog = 'backlog',
}

const sections: RecordSection[] = [
  { id: IterationSections.Overview, label: 'Overview' },
  { id: IterationSections.Backlog, label: 'Backlog' },
]

const PlanningIntervalIterationPage = (props: {
  params: Promise<{ key: string; iterationKey: string }>
}) => {
  const { key, iterationKey } = use(props.params)
  const piKey = Number(key)
  const piIterationKey = Number(iterationKey)

  // Rendered by the overview once its metrics query resolves, so the
  // iteration's health can sit in the identity bar beside its name.
  const [healthIndicator, setHealthIndicator] = useState<ReactNode>(null)

  const router = useRouter()

  // The active section lives in the URL, owned by RecordLayout. Read here only
  // to gate the backlog, which is the expensive query.
  const searchParams = useSearchParams()
  const activeSection = (searchParams.get('section') ??
    IterationSections.Overview) as IterationSections

  const { data: iteration, isLoading } = useGetPlanningIntervalIterationQuery({
    planningIntervalKey: piKey,
    iterationKey: piIterationKey,
  })

  useDocumentTitle(`${iteration?.name ?? piIterationKey} - PI Iteration`)

  const { data: piIterations } = useGetPlanningIntervalIterationsQuery(piKey)

  const {
    data: backlog,
    isLoading: backlogIsLoading,
    refetch: refetchBacklog,
  } = useGetPlanningIntervalIterationBacklogQuery(
    { planningIntervalKey: piKey, iterationKey: piIterationKey },
    { skip: activeSection !== IterationSections.Backlog },
  )

  const handleIterationChange = (value: string | number) => {
    const params = new URLSearchParams(searchParams.toString())
    router.push(
      `/planning/planning-intervals/${piKey}/iterations/${value}?${params.toString()}`,
    )
  }

  const iterationItems = !piIterations
    ? []
    : [...piIterations]
        .sort(
          (a, b) => new Date(b.start).getTime() - new Date(a.start).getTime(),
        )
        .map((option) => ({ label: option.name, value: option.key }))

  const switchIterations = !iterationItems.length ? null : (
    <IconMenu
      icon={<SwapOutlined />}
      tooltip="Switch to another PI iteration"
      items={iterationItems}
      selectedKeys={[piIterationKey.toString()]}
      onChange={handleIterationChange}
    />
  )

  if (isLoading) return <PlanningIntervalIterationLoading />
  if (!iteration) return notFound()

  const state =
    IterationState[iteration.state as keyof typeof IterationState] ??
    IterationState.Future

  const renderSection = (section: IterationSections) => {
    switch (section) {
      case IterationSections.Backlog:
        return (
          <SprintBacklogGrid
            workItems={backlog ?? []}
            isLoading={backlogIsLoading}
            refetch={refetchBacklog}
            persistStateKey="iteration-backlog"
          />
        )
      default:
        return (
          <PlanningIntervalIterationOverview
            iteration={iteration}
            onHealthIndicatorReady={setHealthIndicator}
          />
        )
    }
  }

  return (
    <RecordLayout
      sections={sections}
      defaultSection={IterationSections.Overview}
      record={{
        name: iteration.name,
        recordKey: String(iteration.key),
        subtitle: 'PI Iteration',
        parent: {
          label: iteration.planningInterval.name,
          href: `/planning/planning-intervals/${iteration.planningInterval.key}`,
        },
        tags: (
          <Flex gap="small" align="center">
            {switchIterations}
            <IterationStateTag state={state} />
          </Flex>
        ),
        actions: healthIndicator,
      }}
      facts={<PlanningIntervalIterationFacts iteration={iteration} />}
    >
      {(section) => renderSection(section as IterationSections)}
    </RecordLayout>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const PlanningIntervalIterationPageWithSuspense = (props: {
  params: Promise<{ key: string; iterationKey: string }>
}) => (
  <Suspense fallback={<PlanningIntervalIterationLoading />}>
    <PlanningIntervalIterationPage {...props} />
  </Suspense>
)

const PlanningIntervalIterationPageWithAuthorization = authorizePage(
  PlanningIntervalIterationPageWithSuspense,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalIterationPageWithAuthorization
