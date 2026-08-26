'use client'

import RisksGrid from '@/src/components/common/planning/risks-grid'
import { useGetPlanningIntervalRisksQuery } from '@/src/store/features/planning/planning-interval-api'
import { useState } from 'react'

export interface PlanningIntervalRisksSectionProps {
  planningIntervalKey: number
}

const PlanningIntervalRisksSection = ({
  planningIntervalKey,
}: PlanningIntervalRisksSectionProps) => {
  const [includeClosed, setIncludeClosed] = useState<boolean>(false)

  const {
    data: risks,
    isLoading,
    refetch,
  } = useGetPlanningIntervalRisksQuery({
    planningIntervalKey,
    includeClosed,
  })

  return (
    <RisksGrid
      risks={risks ?? []}
      updateIncludeClosed={setIncludeClosed}
      isLoadingRisks={isLoading}
      refreshRisks={refetch}
      newRisksAllowed
      hideTeamColumn={false}
      persistStateKey="planning-interval-risks"
    />
  )
}

export default PlanningIntervalRisksSection
