'use client'

import { useDocumentTitle } from '@/src/hooks'
import { use, useState } from 'react'
import { PageTitle } from '@/src/components/common'
import { notFound } from 'next/navigation'
import RisksGrid from '@/src/components/common/planning/risks-grid'
import { authorizePage } from '@/src/components/hoc'
import {
  useGetPlanningIntervalQuery,
  useGetPlanningIntervalRisksQuery,
} from '@/src/store/features/planning/planning-interval-api'

const PlanningIntervalRisksPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const piKey = Number(key)

  useDocumentTitle('PI Risks')
  const [includeClosedRisks, setIncludeClosedRisks] = useState<boolean>(false)

  const { data: planningIntervalData, isLoading } =
    useGetPlanningIntervalQuery(piKey)

  const {
    data: risksData,
    isLoading: risksIsLoading,
    error: risksError,
    refetch: refetchRisks,
  } = useGetPlanningIntervalRisksQuery({
    planningIntervalKey: piKey,
    includeClosed: includeClosedRisks,
  })

  const onIncludeClosedRisksChanged = (includeClosed: boolean) => {
    setIncludeClosedRisks(includeClosed)
  }

  if (!isLoading && !planningIntervalData) {
    return notFound()
  }

  return (
    <>
      <PageTitle title="PI Risks" />
      <RisksGrid
        risks={risksData ?? []}
        updateIncludeClosed={onIncludeClosedRisksChanged}
        isLoadingRisks={risksIsLoading}
        refreshRisks={refetchRisks}
        newRisksAllowed={true}
        hideTeamColumn={false}
        persistStateKey="planning-interval-risks"
      />
    </>
  )
}

const PlanningIntervalRisksPageWithAuthorization = authorizePage(
  PlanningIntervalRisksPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalRisksPageWithAuthorization
