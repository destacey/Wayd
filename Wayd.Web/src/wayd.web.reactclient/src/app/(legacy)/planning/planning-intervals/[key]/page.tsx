'use client'

import { authorizePage } from '@/src/components/hoc'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect } from 'react'
import PlanningIntervalDetailsLoading from './loading'
import { useGetPlanningIntervalQuery } from '@/src/store/features/planning/planning-interval-api'
import { IterationState } from '@/src/components/types'

const PlanningIntervalRootPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const piKey = Number(key)

  const router = useRouter()
  const { data: planningIntervalData, isLoading } =
    useGetPlanningIntervalQuery(+piKey)

  const stateId = planningIntervalData?.state?.id as IterationState | undefined
  const landingPage =
    stateId === IterationState.Active || stateId === IterationState.Completed
      ? 'overview'
      : 'details'

  useEffect(() => {
    if (planningIntervalData) {
      router.replace(`/planning/planning-intervals/${piKey}/${landingPage}`)
    }
  }, [planningIntervalData, landingPage, piKey, router])

  if (!isLoading && !planningIntervalData) {
    return notFound()
  }

  return <PlanningIntervalDetailsLoading />
}

const PlanningIntervalRootPageWithAuthorization = authorizePage(
  PlanningIntervalRootPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalRootPageWithAuthorization
